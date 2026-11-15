#!/usr/bin/env bash
#
# Checks that every Rasa.NET MySQL migration has been applied to the Auth, Char and World databases.
# The same check as check-mysql-migrations.ps1, for Linux.
#
# Reads the migration ids from the checked-out repository (src/Rasa.DBL/Migrations/MySqlAuth, MySqlChar and
# MySqlWorld: each migration's [Migration("...")] attribute) and compares them with the ids in each database's
# __EFMigrationsHistory table. Nothing is changed in the databases; it only reads.
#
# For each database it reports:
#   pending  in the repository, not in the database (not applied yet);
#   unknown  in the database, not in the repository (applied from a newer checkout, or an id a later
#            migration renamed and the history kept).
#
# Connection settings are read as the servers read them: databasesettings.json, overlaid with
# databasesettings.env.json if present, overlaid with environment variables (Databases__World__Password and
# the like, as compose.mysql.yml sets them). Reading the JSON files needs jq or python3; settings given
# wholly by environment variables need neither.
#
# Exit code: 0 all applied, 1 something pending, 2 error (no repository, settings, mysql client or connection).
#
# Usage: gametools/check-mysql-migrations.sh [options]
#   --repo DIR        the Rasa.NET checkout (default: the current folder, else the one this script's
#                     gametools folder is in)
#   --settings DIR    the folder holding databasesettings.json (default: <repo>/out/game if it has one,
#                     else <repo>/src/Rasa.DBL)
#   --mysql PATH      the mysql client (default: mysql, else mariadb, on PATH)
#   --databases LIST  which to check, comma-separated: Auth,Char,World (default: all three)

set -u
export LC_ALL=C   # ids compared and sorted byte by byte

repo=""
settings_dir=""
mysql_client=""
databases="Auth,Char,World"

if [ -t 1 ]; then
    red=$'\e[31m'; green=$'\e[32m'; yellow=$'\e[33m'; grey=$'\e[90m'; plain=$'\e[0m'
else
    red=""; green=""; yellow=""; grey=""; plain=""
fi

fail() {
    echo "${red}ERROR: $1${plain}" >&2
    exit 2
}

usage() {
    sed -n '/^# Usage:/,/^$/p' "$0" | sed 's/^# \{0,1\}//'
    exit "$1"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --repo) repo="${2:-}"; shift 2 ;;
        --settings) settings_dir="${2:-}"; shift 2 ;;
        --mysql) mysql_client="${2:-}"; shift 2 ;;
        --databases) databases="${2:-}"; shift 2 ;;
        -h|--help) usage 0 ;;
        *) echo "Unknown option: $1" >&2; usage 2 ;;
    esac
done

is_repo() { [ -n "$1" ] && [ -d "$1/src/Rasa.DBL/Migrations" ]; }

# The repository.
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
if [ -n "$repo" ]; then
    is_repo "$repo" || fail "No src/Rasa.DBL/Migrations under '$repo'."
elif is_repo "$PWD"; then
    repo="$PWD"
elif is_repo "$(dirname "$script_dir")"; then
    repo="$(dirname "$script_dir")"
else
    fail "Run this from the Rasa.NET folder, or pass --repo <path to Rasa.NET>."
fi
repo="$(cd "$repo" && pwd)"

# The databases asked for.
IFS=',' read -r -a wanted <<< "$databases"
for database in "${wanted[@]}"; do
    case "$database" in
        Auth|Char|World) ;;
        *) fail "Unknown database '$database': use Auth, Char or World." ;;
    esac
done

# The settings.
if [ -z "$settings_dir" ]; then
    if [ -f "$repo/out/game/databasesettings.json" ]; then
        settings_dir="$repo/out/game"
    else
        settings_dir="$repo/src/Rasa.DBL"
    fi
fi
settings_file="$settings_dir/databasesettings.json"
env_file="$settings_dir/databasesettings.env.json"
[ -f "$settings_file" ] || settings_file=""
[ -f "$env_file" ] || env_file=""
from_environment=""
if env | grep -q '^Databases__'; then from_environment=yes; fi
if [ -z "$settings_file" ] && [ -z "$from_environment" ]; then
    fail "No databasesettings.json in '$settings_dir', and no Databases__* environment variables."
fi
if { [ -n "$settings_file" ] || [ -n "$env_file" ]; } && ! command -v jq >/dev/null 2>&1 && ! command -v python3 >/dev/null 2>&1; then
    fail "Reading databasesettings.json needs jq or python3. Install one, or give the settings as Databases__* environment variables."
fi

# One value of a JSON file's Databases.<database>.<key>, empty if it has none.
json_value() {
    local file="$1" database="$2" key="$3"
    if command -v jq >/dev/null 2>&1; then
        jq -r --arg d "$database" --arg k "$key" '.Databases[$d][$k] // empty | tostring' "$file"
    elif command -v python3 >/dev/null 2>&1; then
        python3 -I -c '
import json, sys
value = json.load(open(sys.argv[1], encoding="utf-8-sig")).get("Databases", {}).get(sys.argv[2], {}).get(sys.argv[3])
if value is not None:
    sys.stdout.write(str(value))
' "$file" "$database" "$key"
    else
        fail "Reading $file needs jq or python3. Install one, or give the settings as Databases__* environment variables."
    fi
}

# A setting, as the servers resolve it: the environment, then databasesettings.env.json, then databasesettings.json.
setting() {
    local database="$1" key="$2" variable="Databases__${1}__${2}" value=""
    if [ -n "${!variable+set}" ]; then
        printf '%s' "${!variable}"
        return
    fi
    if [ -n "$env_file" ]; then value="$(json_value "$env_file" "$database" "$key")" || exit 2; fi
    if [ -z "$value" ] && [ -n "$settings_file" ]; then value="$(json_value "$settings_file" "$database" "$key")" || exit 2; fi
    printf '%s' "$value"
}

# The mysql client.
if [ -z "$mysql_client" ]; then
    mysql_client="$(command -v mysql || command -v mariadb || true)"
fi
[ -n "$mysql_client" ] && [ -x "$mysql_client" ] || fail "mysql client not found. Install it (mysql-client or mariadb-client), or pass --mysql <path>."

# Runs one query against a database; prints its rows, or mysql's own error to stderr and returns non-zero.
query() {
    local database="$1" sql="$2" options status errors
    options="$(mktemp)" || fail "Could not make a temporary file."
    errors="$(mktemp)" || fail "Could not make a temporary file."
    chmod 600 "$options"
    local password
    password="$(setting "$database" Password)"
    password="${password//\\/\\\\}"
    password="${password//\"/\\\"}"
    {
        echo "[client]"
        echo "host=$(setting "$database" Host)"
        echo "port=$(setting "$database" Port)"
        echo "user=$(setting "$database" User)"
        echo "password=\"$password\""
    } > "$options"
    "$mysql_client" --defaults-extra-file="$options" --batch --skip-column-names \
        --database="$(setting "$database" Database)" --execute="$sql" 2> "$errors"
    status=$?
    if [ $status -ne 0 ]; then
        local message
        message="$(grep -m1 '^ERROR' "$errors")"
        [ -n "$message" ] || message="$(tr '\n' ' ' < "$errors")"
        case "$message" in
            *"ERROR 1146"*) message="it has no __EFMigrationsHistory table: no migration was ever applied there, or this is not the right database. ($message)" ;;
        esac
        echo "$message" >&2
    fi
    rm -f "$options" "$errors"
    return $status
}

echo "Repository: $repo"
if command -v git >/dev/null 2>&1; then
    head="$(git -C "$repo" log --oneline -1 2>/dev/null)"
    [ -n "$head" ] && echo "Checked out: $head"
fi
sources=()
[ -n "$settings_file" ] && sources+=("$settings_file")
[ -n "$env_file" ] && sources+=("databasesettings.env.json")
[ -n "$from_environment" ] && sources+=("Databases__* environment variables")
joined="$(printf '%s + ' "${sources[@]}")"
echo "Settings:   ${joined% + }"
echo "Client:     $mysql_client"
echo

pending_total=0
errors=0
work="$(mktemp -d)" || fail "Could not make a temporary folder."
trap 'rm -rf "$work"' EXIT

for database in "${wanted[@]}"; do
    folder="$repo/src/Rasa.DBL/Migrations/MySql$database"
    [ -d "$folder" ] || fail "No migrations folder '$folder'."
    grep -ho '\[Migration("[^"]*")\]' "$folder"/*.cs | sed 's/^\[Migration("\(.*\)")\]$/\1/' | LC_ALL=C sort -u > "$work/expected"

    label="$database ($(setting "$database" Database) on $(setting "$database" Host):$(setting "$database" Port))"

    if ! applied_rows="$(query "$database" 'SELECT MigrationId FROM __EFMigrationsHistory' 2> "$work/error")"; then
        echo "${red}${label}${plain}"
        echo "${red}  could not read __EFMigrationsHistory: $(cat "$work/error")${plain}"
        echo
        errors=$((errors + 1))
        continue
    fi
    printf '%s\n' "$applied_rows" | sed '/^$/d' | LC_ALL=C sort -u > "$work/applied"

    LC_ALL=C comm -23 "$work/expected" "$work/applied" > "$work/pending"
    LC_ALL=C comm -13 "$work/expected" "$work/applied" > "$work/unknown"
    latest_applied="$(LC_ALL=C comm -12 "$work/expected" "$work/applied" | tail -n 1)"

    total=$(wc -l < "$work/expected")
    pending=$(wc -l < "$work/pending")
    colour="$green"
    [ "$pending" -gt 0 ] && colour="$yellow"
    echo "${colour}${label}${plain}"
    echo "${colour}  $((total - pending)) of $total applied, $pending pending${plain}"

    while IFS= read -r id; do
        note=""
        if [ -n "$latest_applied" ] && [[ "$id" < "$latest_applied" ]]; then
            note="   (older than the newest applied one: a migration landed with an earlier timestamp)"
        fi
        echo "${yellow}    pending  $id$note${plain}"
    done < "$work/pending"
    while IFS= read -r id; do
        echo "${grey}    unknown  $id   (in the database, not in this checkout)${plain}"
    done < "$work/unknown"
    echo
    pending_total=$((pending_total + pending))
done

if [ $errors -gt 0 ]; then
    echo "${red}Could not check $errors database(s).${plain}"
    exit 2
fi
if [ $pending_total -gt 0 ]; then
    echo "${yellow}$pending_total migration(s) pending.${plain}"
    exit 1
fi
echo "${green}All MySQL migrations are applied.${plain}"
exit 0
