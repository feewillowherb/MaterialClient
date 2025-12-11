using System.Reactive.Linq;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests;

public class WeightScaleRxTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public WeightScaleRxTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestWeightScaleDataProcessing()
    {
        // 模拟设备：从-1到1000，步长0.01，每50ms更新一次数据
        var deviceObservable = Observable.Generate(
            initialState: -1.0,
            condition: weight => weight <= 1000,
            iterate: weight => weight + 0.01,
            resultSelector: weight => weight,
            timeSelector: _ => TimeSpan.FromMilliseconds(50)
        );

        // 消费者：每200ms处理一次数据
        var processedData = new List<double>();
        var subscription = deviceObservable
            .Sample(TimeSpan.FromMilliseconds(200))  // 至少200ms处理一次
            //.Where(weight => weight % 50 == 0)       // 过滤能被50整除的数据
            .Subscribe(weight =>
            {
                _testOutputHelper.WriteLine($"处理数据: {weight}");
                processedData.Add(weight);
            });

        // 等待所有数据处理完成
        // 总数据量：(1000 - (-1)) / 0.01 + 1 = 100101 个数据点
        // 总时间：100101 * 50ms = 约 5005秒 (太长了，实际测试中我们会限制范围)
        await Task.Delay(TimeSpan.FromSeconds(10));
        
        subscription.Dispose();

        // 验证：确保能被50整除的数据被处理
        //Assert.NotEmpty(processedData);
        //Assert.All(processedData, weight => Assert.Equal(0, weight % 50));
    }

    [Fact]
    public async Task TestWeightScaleDataProcessing_ShortVersion()
    {
        // 短版本测试：从-1到100，步长0.01，每50ms更新一次数据
        var deviceObservable = Observable.Generate(
            initialState: -1.0,
            condition: weight => weight <= 100,
            iterate: weight => weight + 0.01,
            resultSelector: weight => weight,
            timeSelector: _ => TimeSpan.FromMilliseconds(50)
        );

        // 消费者：每200ms处理一次数据
        var processedData = new List<double>();
        var completionSource = new TaskCompletionSource<bool>();
        
        var subscription = deviceObservable
            .Sample(TimeSpan.FromMilliseconds(200))  // 至少200ms处理一次
            .Where(weight => Math.Abs(weight % 50) < 0.01)  // 过滤能被50整除的数据（考虑浮点数精度）
            .Subscribe(
                onNext: weight =>
                {
                    Console.WriteLine($"处理数据: {weight:F2}");
                    processedData.Add(weight);
                },
                onError: ex => completionSource.SetException(ex),
                onCompleted: () => completionSource.SetResult(true)
            );

        // 等待处理完成
        await completionSource.Task;
        
        subscription.Dispose();

        // 验证：确保有数据被处理
        Assert.NotEmpty(processedData);
        foreach (var weight in processedData)
        {
            Console.WriteLine($"验证数据: {weight:F2}");
        }
    }
}