# Abilities

How player abilities work on this server, where their numbers come from, and what is and is
not resolved yet.

## The data

Everything an ability is comes from six world tables, which are the client's
`generated.client.actiondata` row for row. The client reads the same tables into
`ActorActionInfo` (`client/actions/__init__.py`), so the two sides agree by construction.

| table | keyed by | holds |
| --- | --- | --- |
| `action` | action id | the `ActionId` name and the client module that runs it (`abilities.lightning`) |
| `action_level` | action, level | windup ms, recovery ms, max range, reuse ms (cooldown), preload, `start_reuse_on_perform` |
| `action_cost` | action, level | attribute (5 chi/adrenaline, 6 power) and amount |
| `action_property` | action, level | an `AbilityProperty` and its value |
| `action_item_requirement` | action, level | item class and count the performer must carry |
| `item_template_action` | item template | the action and level a usable item performs |

`AbilityProperty` (`Data/AbilityProperty.cs`) names all 129 properties the client defines:
`DamageAmountMin/Max`, `HealAmountMin/Max`, `Duration`, `Interval`, `RadiusAroundSource`,
`RadiusAroundTarget`, `ConeRadius`, `StunChance`, `KnockbackDistance`,
`DrainPerTickAdrenaline`, and so on. Amounts are base values at level 1.

### Scaling

Damage, healing and costs scale with the performer's level by `DAMAGE_SCALE_TYPE` /
`CONSUMABLE_SCALE_TYPE` (`shared/scaling.py`):

- type 1, linear: `base / 100 * (100 + (level - 1) * 100 / 3)` - a third more per level;
- type 2, exponential: `base * 2 ^ ((level - 1) / 8)` - doubling every eight levels;
- anything else: unscaled.

`AbilityManager.Scale` is that function.

### An example: Lightning (action 194)

| level | damage | power | cooldown | range | also |
| --- | --- | --- | --- | --- | --- |
| 1 | 180-240 | 25 | 1200 ms | 60 | - |
| 2 | 240-300 | 50 | 1200 ms | 60 | arc 210, radius 12 |
| 3 | 240-300 | 75 | 1200 ms | 60 | +50% sonic |
| 4 | 240-300 | 100 | 1200 ms | 60 | 50% stun for 3 s |
| 5 | 240-300 | 150 | 1200 ms | 60 | 60-90 every 2 s for 6 s |

Windup 500 ms, recovery 700 ms, damage type 13 (electrical), scale type 2. The server used
to roll a flat 233-311 at every level.

## The exchange with the client

From `client/actions/baseactoraction.py` and `abilities/baseactorability.py`:

1. The client checks the ability itself - cooldown, cost, target, range - and if it passes,
   plays its own windup and sends `RequestPerformAbility(actionId, level, target, itemId)`.
   It does not wait for the server to start the windup.
2. `AbilityManager.RequestPerformAbility` checks the same things. A refusal is answered with
   `UserActionFailed(actionId, level, msgId)`; the client shows the message and cancels its
   windup. An accepted request sends `PerformWindup` to everyone *else* (the performer's
   client already played it) and queues the action for the windup time.
3. When the windup has run, `AbilityManager.PerformRecovery` takes the cost, starts the
   cooldown, applies the effect and sends `PerformRecovery(actionId, level, hits, misses,
   missdata, hitdata)` to everyone including the performer, whose client runs the ability's
   `DoAbility` over the lists to show the numbers.

An interrupted windup (`RequestActionInterrupt`: the player moved) costs nothing and starts no
cooldown; the others are sent `ActionInterrupt`.

### What is checked

In order: the action and level exist; the player is alive; the player owns it (a skill of
theirs grants the ability at that level or higher, or the item they are using performs
exactly that action); the server can resolve it; it is off cooldown; the target exists, is
alive and on this map; the target is within `max_range` (+2.5 m of slack for a moving
target); a damage ability's target is hostile (a non-AFS creature); the costs can be paid;
the required items are carried.

### Cooldowns

Per actor and action id, in `Actor.ActionReuseUntil`, by `Environment.TickCount64`. When
`start_reuse_on_perform` is set the client starts its own timer at recovery + reuse from the
moment the windup ends; the server counts the same, 150 ms shorter so its clock never says
"no" while the client's says "ready". When it is clear the server tells the client to start
with `ActionReuseTimerRestarted`.

### Costs

Paid on landing, not on asking. `UpdatePower` / `UpdateChi` go to the performer's client.

## What is resolved

- **Direct damage** for abilities whose client class is a `DamageBase`: lightning, force
  blast (knockback), rushing blow, tectonic strike, vortex, shrapnel, explosive wave,
  concussive wave. Each target gets its own roll of `DAMAGE_AMOUNT_MIN..MAX`, scaled,
  applied armour-first through `ActorManager.Damage`; the hit data carries the client's
  12-field `rawInfo` per target. Targets are the single target, everything hostile within
  `RADIUS_AROUND_TARGET` of it, or everything within `RADIUS_AROUND_SOURCE` / `CONE_RADIUS` of
  the performer. A cone is taken as the full circle for now.
- **Sprint**: a game effect with the real numbers - `EFFECT_MOVEMENT_MODIFIER` as a percent
  (120 at level 1 to 160 at 5), `DURATION` as the cap, and `DRAIN_PER_TICK_ADRENALINE /
  (INTERVAL * 10)` chi a second, which is how the client shows it (`abilities/sprint.py`).
  It ends when the duration is up, the adrenaline is gone, the player right-clicks the buff
  away (`RequestDetachGameEffect`), or the player presses sprint again. That last one is the
  server's doing: `SprintAction` does not set `isToggle`, so a second press is a second
  request, and `RequestPerformAbility` answers it by ending the running sprint and refusing
  silently (a `UserActionFailed` with no message), which cancels the client's local windup
  and takes nothing. The old code gave sprint 500 ms and 210-250% speed.

Anything else - heals, buffs, debuffs, summons, traps, damage abilities of other client
classes - is refused with "cannot perform action now" and logged once, so it costs the player
nothing and the gap shows in the log. Creature abilities keep their own path
(`creature_action`, `BehaviorManager`).

## Regeneration

Abilities spend power and chi, so both have to come back. `ActorManager.Regenerate` runs once
a second for every player on a map and adds each attribute's `RefreshAmount` to health,
armour, power and chi once every `RefreshPeriod` seconds, up to the maximum. Nothing is sent
for it: the client predicts the same thing from the amount and period in its last update, so
both sides move together and the next real update - a cost, a hit - carries the exact value.
Without the server-side tick the server's copy of a bar never moved, so a cost check read a
power bar that was full at map entry and only ever went down.

Health and armour rates come from `UpdateStatsValues` (the original rule and the worn
armour's summed `regen_rate`), and their period is five times longer in combat
(`CombatRegen`); the tick honours that. Power and chi regenerate at the health rate as an
interim rule - the live game grew adrenaline from combat and regenerated power by a formula
of its own, neither of which survives, and with no regeneration at all an ability could be
used a handful of times per map. A damaging ability puts the performer in combat the way a
weapon hit does.

## Game effects

`GameEffect` now times itself by `Environment.TickCount64` (`ExpiresTick`, `NextTickTick`).
The worker runs on a 500 ms timer but used to age effects by the world-loop delta, so a
"5 second" effect ran for most of a minute. An effect flagged `AllowDetach` can be removed at
the player's request; gestures and sprint are, debuffs are not. Effects are cleared when the
player leaves the map.
