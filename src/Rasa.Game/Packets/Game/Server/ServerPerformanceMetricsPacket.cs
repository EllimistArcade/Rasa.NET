namespace Rasa.Packets.Game.Server
{
    using Data;
    using Memory;

    /// <summary>
    /// How the world loop is doing, for the client's diagnostics display.
    /// clientmethod.Recv_ServerPerformanceMetrics(avgLoopsPerSecond, peakLoopMs) hands both to
    /// gameclient.SetServerPerfMetrics, alongside SetNetworkResponseTime and the rest of the
    /// network readouts.
    ///
    /// Both go out as doubles, because both parameters are floats. The binding is Boost.Python,
    /// and the caller object it builds carries its signature in its RTTI name:
    ///
    ///   caller&lt;P8GameClient@TRasa@@AEXMM@Z, default_call_policies,
    ///          mpl::vector4&lt;X, AAVGameClient@TRasa@@, M, M&gt;&gt;
    ///
    /// which is void TRasa::GameClient::SetServerPerfMetrics(float, float) written twice - once
    /// as a member function pointer and once as an mpl vector of return type, this, and the two
    /// arguments. An int would be accepted too, since Boost.Python converts one for a float
    /// parameter, but it would throw away the fraction on a measurement that has one.
    ///
    /// In 1.16.5.0 the client does nothing with it. The registration hands Boost.Python the
    /// member function at 0x4d74a0, and that function is one instruction, "ret 8" - it pops its
    /// two floats and returns, which is also a third confirmation of the signature. It sits in a
    /// block of stubs with EnableDiagnostics (0x4d7480, ret 4), DisplayNetworkRTT (0x4d74b0,
    /// ret 4) and DisplayNetworkVariance (0x4d74c0, ret 4), and DiagnosticsEnabled beside them
    /// (0x4d7490) is "xor al, al; ret", so it always answers false and the overlay these feed
    /// cannot be opened at all. Five of the 110 gameclient bindings are stubs and four of them
    /// are this group: the diagnostics were taken out of the retail build.
    ///
    /// The message is kept because it is what the protocol says and it costs nothing to have,
    /// but GameConfig.PerformanceMetricsInterval is 0 by default so none are sent. The numbers
    /// are still worth having - 'perf' on the console reads them.
    /// </summary>
    public class ServerPerformanceMetricsPacket : ServerPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.ServerPerformanceMetrics;

        public double AvgLoopsPerSecond { get; }
        public double PeakLoopMs { get; }

        public ServerPerformanceMetricsPacket(double avgLoopsPerSecond, double peakLoopMs)
        {
            AvgLoopsPerSecond = avgLoopsPerSecond;
            PeakLoopMs = peakLoopMs;
        }

        public override void Write(PythonWriter pw)
        {
            pw.WriteTuple(2);
            pw.WriteDouble(AvgLoopsPerSecond);
            pw.WriteDouble(PeakLoopMs);
        }
    }
}
