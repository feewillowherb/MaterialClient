using Volo.Abp.Application.Dtos;

namespace MaterialClient.Common.Models;

/// <summary>
/// 获取称重列表项的请求参数
/// </summary>
public class GetWeighingListItemsInput : PagedAndSortedResultRequestDto
{
    /// <summary>
    /// 是否已完成：null=全部, true=已完成, false=未完成
    /// </summary>
    public bool? IsCompleted { get; set; }
    
    public GetWeighingListItemsInput()
    {
        // 默认按 JoinTime 降序排列
        Sorting = "JoinTime DESC";
    }
}
