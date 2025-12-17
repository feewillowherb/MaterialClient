using System.Threading.Tasks;
using MaterialClient.Common.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Services;

public interface IWaybillService
{
    /// <summary>
    /// 完成运单（将 OrderType 设置为 Completed）
    /// </summary>
    /// <param name="waybillId">运单ID</param>
    Task CompleteOrderAsync(long waybillId);
}

/// <summary>
/// 运单服务
/// </summary>
[AutoConstructor]
public partial class WaybillService : DomainService, IWaybillService
{
    private readonly IRepository<Waybill, long> _waybillRepository;

    [UnitOfWork]
    public async Task CompleteOrderAsync(long waybillId)
    {
        var waybill = await _waybillRepository.GetAsync(waybillId);
        waybill.OrderTypeCompleted();
        await _waybillRepository.UpdateAsync(waybill);
    }
}

