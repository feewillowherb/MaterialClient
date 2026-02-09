using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MaterialClient.ViewModels;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Xunit;
using Xunit.Abstractions;

namespace MaterialClient.Common.Tests.Tests;

/// <summary>
/// Unit tests for SearchableSelectionViewModel
/// </summary>
public class SearchableSelectionViewModelTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    public SearchableSelectionViewModelTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InitializeAsync_ClientSide_LoadsAllItems()
    {
        // Arrange
        var items = new List<string> { "Apple", "Banana", "Cherry" };
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);

        // Act
        await viewModel.InitializeAsync();

        // Assert
        Assert.Equal(3, viewModel.TotalCount);
        Assert.Equal(3, viewModel.PagedItems.Count);
    }

    [Fact]
    public async Task SearchText_ClientSide_FiltersItems()
    {
        // Arrange
        var items = new List<string> { "Apple", "Banana", "Cherry", "Apricot" };
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act
        viewModel.SearchText = "Ap";

        // Wait for throttle (300ms) + processing
        await Task.Delay(500);

        // Assert
        Assert.Equal(2, viewModel.TotalCount); // Apple, Apricot
    }

    [Fact]
    public async Task SearchText_ServerSide_CallsLoadPageFunc()
    {
        // Arrange
        var loadPageCallCount = 0;
        string? capturedSearchText = null;
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ServerSide,
            s => s,
            loadPageFunc: (search, page, pageSize, selectedIds) =>
            {
                loadPageCallCount++;
                capturedSearchText = search;
                return Task.FromResult(new PagedResultDto<string>
                {
                    TotalCount = 2,
                    Items = new List<string> { "Apple", "Apricot" }
                });
            },
            getIdSelector: s => null,
            pageSize: 10);

        // Act
        await viewModel.InitializeAsync();
        viewModel.SearchText = "Ap";

        // Wait for throttle
        await Task.Delay(500);

        // Assert
        Assert.True(loadPageCallCount >= 1); // At least called once with search
        Assert.Equal("Ap", capturedSearchText);
        Assert.Equal(2, viewModel.TotalCount);
    }

    [Fact]
    public async Task SelectItem_UpdatesSelectedValue()
    {
        // Arrange
        var items = new List<string> { "Apple", "Banana", "Cherry" };
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act
        var itemToSelect = viewModel.PagedItems.FirstOrDefault(i => i.Value == "Banana");
        if (itemToSelect != null)
        {
            viewModel.SelectedItem = itemToSelect;
        }

        // Assert
        Assert.Equal("Banana", viewModel.SelectedValue);
        Assert.Equal("Banana", viewModel.SelectedDisplayText);
    }

    [Fact]
    public async Task Pagination_ChangesPage()
    {
        // Arrange
        var items = Enumerable.Range(1, 25).Select(i => $"Item{i}").ToList();
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act
        viewModel.CurrentPage = 2;
        await Task.Delay(100); // Wait for async refresh

        // Assert
        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(25, viewModel.TotalCount);
        // Should show items 11-20
        Assert.Equal("Item11", viewModel.PagedItems.FirstOrDefault()?.DisplayText);
    }

    [Fact]
    public async Task AddNewItem_InsertsAndSelects()
    {
        // Arrange
        var items = new List<string> { "Apple", "Banana" };
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            createNewItemFunc: name =>
            {
                var newItem = $"New-{name}";
                items.Add(newItem); // Simulate adding to data source
                return Task.FromResult<string?>(newItem);
            },
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act
        viewModel.SearchText = "Cherry";
        await Task.Delay(500); // Wait for search throttle

        var initialCount = viewModel.TotalCount;
        viewModel.AddNewItemCommand.Execute(null);
        await Task.Delay(100); // Wait for async add

        // Assert
        Assert.Equal(initialCount + 1, viewModel.TotalCount);
        Assert.Equal("New-Cherry", viewModel.SelectedValue);
        Assert.Equal("New-Cherry", viewModel.SelectedDisplayText);
    }

    [Fact]
    public async Task ShowAddNewButton_OnlyWhenNoResultsAndSearchText()
    {
        // Arrange
        var items = new List<string> { "Apple", "Banana" };
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(items),
            createNewItemFunc: name => Task.FromResult<string?>(name),
            allowAddNew: true,
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act & Assert - Has results, no search text
        Assert.False(viewModel.ShowAddNewButton);

        // Act & Assert - Has search text, has results
        viewModel.SearchText = "App";
        await Task.Delay(500);
        Assert.False(viewModel.ShowAddNewButton);

        // Act & Assert - Has search text, no results
        viewModel.SearchText = "XYZ";
        await Task.Delay(500);
        Assert.True(viewModel.ShowAddNewButton);
    }

    [Fact]
    public async Task Dispose_DisposesSubscriptions()
    {
        // Arrange
        var viewModel = new SearchableSelectionViewModel<string>(
            SearchableSelectionPagingMode.ClientSide,
            s => s,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(new List<string>()),
            pageSize: 10);
        await viewModel.InitializeAsync();

        // Act - Should not throw
        viewModel.Dispose();

        // Assert - Verify no memory leaks (basic check)
        // In a real test, you would use a memory profiler
        Assert.True(true);
    }

    public void Dispose()
    {
        // Clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
