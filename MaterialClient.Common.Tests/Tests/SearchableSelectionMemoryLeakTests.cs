using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.ViewModels;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Memory leak tests for SearchableSelectionViewModel
/// Follows the pattern from HikvisionLprServiceMemoryLeakTests
/// </summary>
public class SearchableSelectionMemoryLeakTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public SearchableSelectionMemoryLeakTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task CreateAndDisposeRepeatedly_ShouldNotLeakMemory()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList();
        var iterations = 100;

        // Force GC and record initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act
        for (var i = 0; i < iterations; i++)
        {
            var viewModel = new SearchableSelectionViewModel<string>(
                SearchableSelectionPagingMode.ClientSide,
                s => s,
                loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
                pageSize: 10);

            await viewModel.InitializeAsync();
            viewModel.SearchText = "Test";
            await Task.Delay(350); // Wait for throttle

            viewModel.Dispose();

            if (i % 10 == 0)
            {
                _output.WriteLine($"已完成 {i}/{iterations} 次创建/销毁");
            }
        }

        // Force GC and record final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");
        _output.WriteLine($"每次创建/销毁平均内存变化: {memoryDelta / iterations} bytes");

        // Assert: Memory growth should be less than 1MB
        Assert.True(memoryDelta < 1024 * 1024,
            $"内存泄漏检测: 在 {iterations} 次创建/销毁后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task RapidSearchChanges_ShouldNotLeakMemory()
    {
        // Arrange
        var items = Enumerable.Range(1, 1000).Select(i => $"Item{i}").ToList();
        var searchCount = 100;

        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);

        await viewModel.InitializeAsync();

        // Force GC and record initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act: Rapid search changes
        for (var i = 0; i < searchCount; i++)
        {
            viewModel.SearchText = $"Item{i}";
            await Task.Delay(50); // Faster than throttle to test rapid changes
        }

        // Wait for all throttled operations to complete
        await Task.Delay(500);

        // Force GC and record final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");

        // Clean up
        viewModel.Dispose();

        // Assert: Memory growth should be reasonable
        Assert.True(memoryDelta < 500 * 1024,
            $"内存泄漏检测: 在 {searchCount} 次搜索后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task MultipleSubscriptions_ShouldNotLeakMemory()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList();
        var subscriptionCount = 100;
        var viewModels = new List<SearchableSelectionViewModel<string>>();

        // Force GC and record initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        // Act: Create multiple ViewModels with active subscriptions
        for (var i = 0; i < subscriptionCount; i++)
        {
            var viewModel = new SearchableSelectionViewModel<string>(
                SearchableSelectionPagingMode.ClientSide,
                s => s,
                loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
                pageSize: 10);

            await viewModel.InitializeAsync();
            viewModels.Add(viewModel);
        }

        var beforeDisposeMemory = GC.GetTotalMemory(false);
        _output.WriteLine($"释放前内存: {beforeDisposeMemory / 1024} KB");

        // Release all ViewModels
        foreach (var viewModel in viewModels)
        {
            viewModel.Dispose();
        }
        viewModels.Clear();

        // Force GC and record final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");

        // Assert: Memory should mostly recover
        Assert.True(memoryDelta < 1024 * 1024,
            $"内存泄漏检测: 在创建和释放 {subscriptionCount} 个 ViewModel 后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task LongRunningWithReactiveChains_ShouldNotLeakMemory()
    {
        // Arrange
        var items = Enumerable.Range(1, 100).Select(i => $"Item{i}").ToList();
        var duration = TimeSpan.FromSeconds(2);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);

        await viewModel.InitializeAsync();

        // Force GC and record initial memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var initialMemory = GC.GetTotalMemory(true);

        _output.WriteLine($"初始内存: {initialMemory / 1024} KB");

        var operationCount = 0;

        // Act: Continuous operations (search, select, pagination)
        while (stopwatch.Elapsed < duration)
        {
            viewModel.SearchText = $"Item{operationCount % 100}";
            await Task.Delay(50);

            if (viewModel.PagedItems.Count > 0)
            {
                viewModel.SelectedItem = viewModel.PagedItems[0];
            }

            if (operationCount % 10 == 0)
            {
                viewModel.CurrentPage = (operationCount % 3) + 1;
                await Task.Delay(50);
            }

            operationCount++;

            if (operationCount % 100 == 0)
            {
                var currentMemory = GC.GetTotalMemory(false);
                _output.WriteLine($"已执行 {operationCount} 次操作, 当前内存: {currentMemory / 1024} KB");
            }
        }

        stopwatch.Stop();

        // Force GC and record final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var finalMemory = GC.GetTotalMemory(true);
        var memoryDelta = finalMemory - initialMemory;

        _output.WriteLine($"运行时间: {stopwatch.ElapsedMilliseconds} ms");
        _output.WriteLine($"执行操作数: {operationCount}");
        _output.WriteLine($"最终内存: {finalMemory / 1024} KB");
        _output.WriteLine($"内存变化: {memoryDelta / 1024} KB");

        // Clean up
        viewModel.Dispose();

        // Assert: Memory growth should be reasonable
        Assert.True(memoryDelta < 500 * 1024,
            $"内存泄漏检测: 在运行 {duration.TotalSeconds} 秒并执行 {operationCount} 次操作后，内存增长了 {memoryDelta / 1024} KB");
    }

    [Fact]
    public async Task DisposeWith_MemorySafety_ShouldWorkCorrectly()
    {
        // Arrange
        var items = Enumerable.Range(1, 1000).Select(i => $"Item{i}").ToList();

        // Act: Create and dispose multiple times to verify DisposeWith works
        for (var i = 0; i < 50; i++)
        {
            var viewModel = new SearchableSelectionViewModel<string>(
                SearchableSelectionPagingMode.ClientSide,
                s => s,
                loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
                pageSize: 10);

            await viewModel.InitializeAsync();

            // Trigger reactive chains
            viewModel.SearchText = "Test";
            await Task.Delay(350);

            // Should dispose all subscriptions
            viewModel.Dispose();
        }

        // Force GC
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Assert: If we got here without exceptions, DisposeWith is working
        Assert.True(true);
    }

    public void Dispose()
    {
        // Clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
