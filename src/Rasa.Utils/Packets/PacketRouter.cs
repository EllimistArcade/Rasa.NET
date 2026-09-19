using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Rasa.Packets
{
    public class PacketRouter<T, O>
        where T : class
        where O : struct
    {
        private readonly Dictionary<O, PacketData> _handlers = new Dictionary<O, PacketData>();

        public PacketRouter()
        {
            SetupHandlers();
        }

        public void SetupHandlers()
        {
            var info = typeof(T).GetTypeInfo();

            foreach (var method in info.DeclaredMethods)
            {
                foreach (var attr in method.GetCustomAttributes<PacketHandlerAttribute>())
                {
                    var paramType = method.GetParameters().FirstOrDefault()?.ParameterType;
                    if (paramType == null)
                        throw new Exception($"Invalid PacketHandler attribute usage! Used on function: {info.FullName}.{method.Name}");

                    var opcode = attr.GetOpcode<O>();

                    // Dictionary.Add says "An item with the same key has already been added",
                    // which names neither the opcode nor either of the two methods fighting
                    // over it, and this runs in a static initialiser at startup.
                    if (_handlers.TryGetValue(opcode, out var existing))
                        throw new Exception($"Two handlers for opcode {opcode}: {info.FullName}.{existing.Name} and {info.FullName}.{method.Name}");

                    _handlers.Add(opcode, new PacketData(paramType, Compile(method, paramType), method.Name));
                }
            }
        }

        /// <summary>
        /// Builds <c>target.Handler((TPacket) packet)</c> and compiles it, so routing a packet is
        /// a delegate call.
        /// </summary>
        /// <remarks>
        /// This was DynamicInvoke on an open Action&lt;T, TPacket&gt;. DynamicInvoke boxes every
        /// argument into an object[], walks the delegate's parameter list and type-checks each one
        /// by reflection, and does it again for every packet from every client - on the MainLoop
        /// thread that also runs the world. It also wraps whatever the handler throws in
        /// TargetInvocationException, so the exception a caller catches, and the first line of
        /// every log about a failing handler, is about the invocation machinery rather than about
        /// what actually went wrong. A compiled delegate has neither problem: the cast is a real
        /// cast in real IL, and an exception comes back out as itself.
        /// </remarks>
        private static Action<T, IOpcodedPacket<O>> Compile(MethodInfo method, Type packetType)
        {
            var target = Expression.Parameter(typeof(T), "target");
            var packet = Expression.Parameter(typeof(IOpcodedPacket<O>), "packet");
            var typed = Expression.Convert(packet, packetType);

            var call = method.IsStatic
                ? Expression.Call(method, target, typed)
                : Expression.Call(target, method, typed);

            return Expression.Lambda<Action<T, IOpcodedPacket<O>>>(call, target, packet).Compile();
        }

        public void RoutePacket(T target, IOpcodedPacket<O> packet)
        {
            // An opcode the server has no packet type for leaves the caller holding null rather
            // than a packet. Routing it would be a NullReferenceException out of the MainLoop.
            if (packet == null)
            {
                Logger.WriteLog(LogType.Error, "PacketRouter was given nothing to route");
                return;
            }

            if (!_handlers.TryGetValue(packet.Opcode, out var handler))
            {
                Logger.WriteLog(LogType.Error, $"PacketRouter can't route to a non-existant opcode: {packet.Opcode}");
                return;
            }

            // A packet whose Opcode property disagrees with the attribute its class is registered
            // under arrives at the wrong handler, and the cast inside it would throw an
            // InvalidCastException from a frame that names neither. Say which is which instead.
            if (!handler.Type.IsInstanceOfType(packet))
            {
                Logger.WriteLog(LogType.Error,
                    $"PacketRouter was given a {packet.GetType().Name} for opcode {packet.Opcode}, which is handled as {handler.Type.Name}");
                return;
            }

            handler.Handler(target, packet);
        }

        public Type GetPacketType(O opcode)
        {
            if (_handlers.TryGetValue(opcode, out var handler))
                return handler.Type;

            Logger.WriteLog(LogType.Error, $"Non-existant PacketRouter type definition! Opcode: {opcode}");
            return null;
        }

        public class PacketData
        {
            public Type Type { get; }
            public Action<T, IOpcodedPacket<O>> Handler { get; }

            /// <summary>The method the handler was compiled from; the compiled delegate itself is a lambda and cannot say.</summary>
            public string Name { get; }

            public PacketData(Type type, Action<T, IOpcodedPacket<O>> handler, string name)
            {
                Type = type;
                Handler = handler;
                Name = name;
            }

            public override string ToString()
            {
                return $"PacketData(Type: {Type} | Handler: {typeof(T).FullName}::{Name})";
            }
        }
    }
}
