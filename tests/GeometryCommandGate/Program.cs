using System;
using static BiofitGeomeTryCommandGate.Decision;

int checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition) throw new Exception("FAIL " + label);
}

var gate = new BiofitGeomeTryCommandGate();
Check(gate.Evaluate(1, 100.000, 100.010, .18) == Accepted, "immediate legacy pair");
Check(gate.Evaluate(1, 100.000, 100.011, .18) == Duplicate, "same command executes at most once");
Check(gate.Evaluate(2, 100.000, 100.181, .18) == Expired, "late paired event not refreshed");
Check(gate.Evaluate(2, 100.200, 100.200, .18) == Duplicate, "expired id cannot be retried");
Check(gate.Evaluate(3, 101.000, 100.800, .18) == Future, "bad clock in future");
Check(gate.Evaluate(4, double.NaN, 101.100, .18) == InvalidClock, "invalid origin");
Check(gate.Evaluate(5, 101.100, double.PositiveInfinity, .18) == InvalidClock, "invalid now");
Check(gate.Evaluate(6, 101.100, 101.100, 0) == InvalidClock, "invalid TTL");
Check(gate.Evaluate(7, 102.000, 101.970, .18) == Accepted, "small time-align lead");
Check(gate.Evaluate(8, 102.000, 102.180, .18) == Accepted, "within TTL boundary");
Check(gate.Evaluate(9, 102.000, 103.000, .18) == Expired, "old BLE backlog discarded");
Check(gate.Evaluate(10, 103.000, 103.001, 1.0) == Accepted, "manual uses same gate");
Check(gate.Evaluate(9, 103.000, 103.001, 1.0) == Duplicate, "out of order id");
Check(gate.LastObservedId == 10, "IDs monotonically consumed");

Console.WriteLine("GeometryCommandGate PASS " + checks + " checks (host only; not Unity or human validation)");
