using Microsoft.ML.OnnxRuntime;
using OnnxStack.Core.Model;

namespace TXT2IMG;

public static class ExecutionProviders
{
    public static OnnxExecutionProvider DirectML(int deviceId = 0)
    {
        return new OnnxExecutionProvider("DirectML", configuration =>
        {
            var sessionOptions = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
            };

            sessionOptions.AppendExecutionProvider_DML(deviceId);
            sessionOptions.AppendExecutionProvider_CPU();
            return sessionOptions;
        });
    }

    public static OnnxExecutionProvider Cpu()
    {
        return new OnnxExecutionProvider("CPU", configuration =>
        {
            var sessionOptions = new SessionOptions
            {
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_DISABLE_ALL
            };

            sessionOptions.AppendExecutionProvider_CPU();
            return sessionOptions;
        });
    }
}
