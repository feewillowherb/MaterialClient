using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace MaterialClient.ViewModels;

/// <summary>
/// Factory methods for creating SearchableSelectionViewModel instances
/// </summary>
public static class SearchableSelectionFactory
{
    /// <summary>
    /// Create a SearchableSelectionViewModel for ABP application service with server-side paging
    /// </summary>
    public static SearchableSelectionViewModel<T> CreateForAbpService<T>(
        Func<T, string> displayTextSelector,
        Func<string?, int, int, IReadOnlyList<int>?, Task<PagedResultDto<T>>> loadPageFunc,
        Func<T, int?> getIdSelector,
        Func<string, Task<T?>>? createNewItemFunc = null,
        ILogger? logger = null,
        int pageSize = 10,
        bool allowAddNew = true)
    {
        return new SearchableSelectionViewModel<T>(
            pagingMode: SearchableSelectionPagingMode.ServerSide,
            displayTextSelector: displayTextSelector,
            logger: logger,
            loadPageFunc: loadPageFunc,
            getIdSelector: getIdSelector,
            createNewItemFunc: createNewItemFunc,
            pageSize: pageSize,
            allowAddNew: allowAddNew);
    }

    /// <summary>
    /// Create a SearchableSelectionViewModel for ABP application service with client-side paging
    /// </summary>
    public static SearchableSelectionViewModel<T> CreateForClientSide<T>(
        Func<T, string> displayTextSelector,
        Func<Task<IReadOnlyList<T>>> loadAllFunc,
        Func<string, Task<T?>>? createNewItemFunc = null,
        ILogger? logger = null,
        int pageSize = 10,
        bool allowAddNew = true)
    {
        return new SearchableSelectionViewModel<T>(
            pagingMode: SearchableSelectionPagingMode.ClientSide,
            displayTextSelector: displayTextSelector,
            logger: logger,
            loadAllFunc: loadAllFunc,
            createNewItemFunc: createNewItemFunc,
            pageSize: pageSize,
            allowAddNew: allowAddNew);
    }

    /// <summary>
    /// Create a SearchableSelectionViewModel from configuration
    /// </summary>
    public static SearchableSelectionViewModel<T> Create<T>(
        ISearchableSelectionConfig<T> config,
        ILogger? logger = null)
    {
        return new SearchableSelectionViewModel<T>(config, logger);
    }

    /// <summary>
    /// Create a SearchableSelectionViewModel for simple string list (client-side)
    /// </summary>
    public static SearchableSelectionViewModel<string> CreateForStrings(
        IEnumerable<string> items,
        ILogger? logger = null,
        int pageSize = 10,
        bool allowAddNew = false)
    {
        var itemList = items.ToList();

        return new SearchableSelectionViewModel<string>(
            pagingMode: SearchableSelectionPagingMode.ClientSide,
            displayTextSelector: s => s,
            logger: logger,
            loadAllFunc: () => Task.FromResult<IReadOnlyList<string>>(itemList),
            pageSize: pageSize,
            allowAddNew: allowAddNew);
    }
}
