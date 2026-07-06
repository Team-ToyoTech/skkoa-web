using SkkoaStudio.Core.Debugging;
using Xunit;

namespace SkkoaStudio.Tests;

public sealed class DebugSessionTests
{
    [Fact]
    public void Debugger_steps_variables_arithmetic_and_output()
    {
        const string source = """
        시작
            변수 x: 정수 = 1
            x = x + 2
            출력 x
        끝
        """;

        SkkoaDebugSession session = new(source);
        session.Start();
        session.StepInto();
        session.StepInto();
        SkkoaDebugSnapshot snapshot = session.StepInto();

        Assert.Equal("3", snapshot.Variables["x"]);
        Assert.Contains("3", snapshot.Output);
    }

    [Fact]
    public void Debugger_steps_function_call()
    {
        const string source = """
        함수 더하기(a: 정수, b: 정수): 정수
            반환 a + b
        끝

        시작
            변수 result: 정수 = 더하기(3, 4)
            출력 result
        끝
        """;

        SkkoaDebugSession session = new(source);
        session.Start();
        SkkoaDebugSnapshot enteredFunction = session.StepInto();
        Assert.Contains("더하기", enteredFunction.CallStack);

        session.StepInto();
        SkkoaDebugSnapshot afterReturn = session.StepInto();
        Assert.Equal("7", afterReturn.Variables["result"]);
    }

    [Fact]
    public void Debugger_continue_stops_runaway_loop()
    {
        const string source = """
        시작
            변수 x: 정수 = 0
            동안 참 반복
                x = x + 1
            끝
        끝
        """;

        SkkoaDebugSession session = new(source);
        session.Start();

        SkkoaDebugSnapshot snapshot = session.Continue();

        Assert.Equal(SkkoaDebugState.Faulted, snapshot.State);
        Assert.Contains("너무 오래", snapshot.Message);
    }
}
