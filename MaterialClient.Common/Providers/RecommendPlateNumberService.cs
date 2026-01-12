using System.Collections.Concurrent;
using MaterialClient.Common.Configuration;
using MaterialClient.Common.Entities;
using MaterialClient.Common.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace MaterialClient.Common.Providers;

/// <summary>
///     ���ƺ��Ƽ�����
///     �����ݿ��м�������200�����ƺŵ��ڴ滺�棬���������õ���С�ַ�����������ƥ���Ƽ�
/// </summary>
public class RecommendPlateNumberService : DomainService, ISingletonDependency
{
    private readonly ILogger<RecommendPlateNumberService> _logger;
    private readonly IRepository<Waybill, long> _waybillRepository;
    private readonly ISettingsService _settingsService;

    private volatile ConcurrentQueue<string> _plateNumberCache = new();

    public RecommendPlateNumberService(
        IRepository<Waybill, long> waybillRepository,
        ILogger<RecommendPlateNumberService> logger,
        ISettingsService settingsService)
    {
        _waybillRepository = waybillRepository;
        _logger = logger;
        _settingsService = settingsService;
    }

    /// <summary>
    ///     ��ʼ�����棬�����ݿ��������200�����ƺ�
    /// </summary>
    [UnitOfWork]
    public async Task InitializeCacheAsync()
    {
        try
        {
            var queryable = await _waybillRepository.GetQueryableAsync();
            var plateNumbers = await queryable
                .Where(w => !string.IsNullOrWhiteSpace(w.PlateNumber))
                .OrderByDescending(w => w.AddDate)
                .Select(w => w.PlateNumber!)
                .Distinct()
                .Take(200)
                .ToListAsync();

            var newCache = new ConcurrentQueue<string>();
            foreach (var plateNumber in plateNumbers)
            {
                newCache.Enqueue(plateNumber);
            }

            _plateNumberCache = newCache;

            _logger.LogInformation(
                "���ƺ��Ƽ����񻺴��ʼ����ɣ������� {Count} �����ƺ�",
                plateNumbers.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "��ʼ�����ƺ��Ƽ����񻺴�ʧ��");
            // ʹ�ÿջ���
            _plateNumberCache = new ConcurrentQueue<string>();
        }
    }

    /// <summary>
    ///     ��������ĳ��ƺţ��ӻ������Ƽ���ƥ��ĳ��ƺ�
    /// </summary>
    /// <param name="plateNumber">����ĳ��ƺ�</param>
    /// <returns>�Ƽ��ĳ��ƺţ����δ�ҵ�ƥ���򷵻�ԭʼ����</returns>
    public string GetRecommendPlateNumber(string plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return plateNumber;

        try
        {
            // ��ȡ��ǰ��������ã�volatile ��ȡ��
            var cache = _plateNumberCache;

            // ��ȡ���õ���С�ַ�������
            var settings = _settingsService.GetSettingsAsync().GetAwaiter().GetResult();
            var minDiffCharCount = settings.SystemSettings.MinDiffCharCount;

            // ������ 0-2 ��Χ��
            if (minDiffCharCount < 0) minDiffCharCount = 0;
            if (minDiffCharCount > 2) minDiffCharCount = 2;

            // �������в���ƥ��
            string? bestMatch = null;
            int bestDiff = int.MaxValue;

            foreach (var cachedPlate in cache)
            {
                if (string.IsNullOrWhiteSpace(cachedPlate))
                    continue;

                var diff = CalculateCharDiff(plateNumber, cachedPlate);

                if (diff <= minDiffCharCount && diff < bestDiff)
                {
                    bestMatch = cachedPlate;
                    bestDiff = diff;
                }
            }

            // ����ҵ�ƥ�䣬��¼��־������
            if (bestMatch != null)
            {
                _logger.LogInformation(
                    "���ƺ��Ƽ�ƥ��ɹ�: ����={InputPlate}, �Ƽ�={RecommendedPlate}, ������={DiffCount}",
                    plateNumber,
                    bestMatch,
                    bestDiff);
                return bestMatch;
            }

            // δ�ҵ�ƥ�䣬����ԭʼ����
            return plateNumber;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "��ȡ�Ƽ����ƺ�ʱ�����쳣: {PlateNumber}", plateNumber);
            return plateNumber;
        }
    }

    /// <summary>
    ///     ���ӳ��ƺŵ����棨�� Waybill ���ʱ���ã�
    /// </summary>
    /// <param name="plateNumber">Ҫ���ӵĳ��ƺ�</param>
    public void AddPlateNumberToCache(string? plateNumber)
    {
        if (string.IsNullOrWhiteSpace(plateNumber))
            return;

        try
        {
            var cache = _plateNumberCache;

            // ��黺���С���������200��������
            if (cache.Count >= 200)
            {
                _logger.LogDebug(
                    "���ƺ��Ƽ����񻺴�������200�������������ӳ��ƺ�: {PlateNumber}",
                    plateNumber);
                return;
            }

            // ����Ƿ��Ѵ��ڣ������ظ���
            if (cache.Contains(plateNumber))
            {
                _logger.LogDebug(
                    "���ƺ��Ѵ����ڻ����У���������: {PlateNumber}",
                    plateNumber);
                return;
            }

            // �����»��沢������Ԫ��
            var newCache = new ConcurrentQueue<string>();
            foreach (var existingPlate in cache)
            {
                newCache.Enqueue(existingPlate);
            }

            newCache.Enqueue(plateNumber);

            // ԭ���滻����
            _plateNumberCache = newCache;

            _logger.LogInformation(
                "�ѽ����ƺ����ӵ��Ƽ����񻺴�: {PlateNumber}, ��ǰ�����С: {Count}",
                plateNumber,
                newCache.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "���ӳ��ƺŵ��Ƽ����񻺴�ʱ�����쳣: {PlateNumber}", plateNumber);
        }
    }

    /// <summary>
    ///     ���������ַ������ַ�������
    ///     �Ƚ������ַ�������ͬλ���ϵĲ�ͬ�ַ����������Ȳ�ͬʱ�������� = ���Ȳ� + λ�ò�������
    /// </summary>
    private int CalculateCharDiff(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return Math.Max(str1?.Length ?? 0, str2?.Length ?? 0);

        int maxLen = Math.Max(str1.Length, str2.Length);
        int diffCount = 0;

        for (int i = 0; i < maxLen; i++)
        {
            char c1 = i < str1.Length ? str1[i] : '\0';
            char c2 = i < str2.Length ? str2[i] : '\0';
            if (c1 != c2) diffCount++;
        }

        return diffCount;
    }
}
