BEGIN TRANSACTION;

-- 1) 清理旧绑定（OldWeighingId）
UPDATE WeighingRecords
SET MatchedId   = NULL,
    WaybillId   = NULL,
    MatchedType = NULL
WHERE Id = :OldWeighingId;

-- 2) 原先与旧绑定配对的记录（PairOtherId）指向新的绑定对象
UPDATE WeighingRecords
SET MatchedId = :NewWeighingId
WHERE Id = :PairOtherId;

-- 3) 将 NewWeighingId 设置为与 PairOtherId 配对，并设置 MatchedType 与 WaybillId
UPDATE WeighingRecords
SET MatchedId   = :PairOtherId,
    MatchedType = 1,
    WaybillId   = :WaybillId
WHERE Id = :NewWeighingId;

-- 4) 更新 Waybill 的 OutTime / OrderTruckWeight / OrderGoodsWeight（直接用子查询读 WeighingRecords）
UPDATE Waybills
SET
    OutTime = (SELECT AddDate FROM WeighingRecords WHERE Id = :NewWeighingId),
    OrderTruckWeight = (SELECT TotalWeight FROM WeighingRecords WHERE Id = :NewWeighingId),
    OrderGoodsWeight = CASE
                           WHEN OrderTotalWeight IS NULL THEN NULL
                           ELSE OrderTotalWeight - (SELECT TotalWeight FROM WeighingRecords WHERE Id = :NewWeighingId)
        END
WHERE Id = :WaybillId;

-- 5) 将附件按序从旧绑定映射到新绑定（此 WITH 只作用于随后的 UPDATE 语句，合法）
WITH
    SourceAttachments AS (
        SELECT wa.AttachmentFileId,
               ROW_NUMBER() OVER (ORDER BY wa.Id ASC) AS rn
        FROM WaybillAttachments wa
                 INNER JOIN WeighingRecordAttachments wra
                            ON wa.AttachmentFileId = wra.AttachmentFileId
                                AND wra.WeighingRecordId = :OldWeighingId
        WHERE wa.WaybillId = :WaybillId
    ),
    TargetAttachments AS (
        SELECT wra.Id AS WraId,
               wra.AttachmentFileId AS CurrentAttachmentFileId,
               ROW_NUMBER() OVER (ORDER BY wra.Id ASC) AS rn
        FROM WeighingRecordAttachments wra
        WHERE wra.WeighingRecordId = :NewWeighingId
    ),
    Mapping AS (
        SELECT t.WraId, s.AttachmentFileId AS NewAttachmentFileId
        FROM TargetAttachments t
                 INNER JOIN SourceAttachments s ON t.rn = s.rn
    )
UPDATE WeighingRecordAttachments
SET AttachmentFileId = (SELECT m.NewAttachmentFileId FROM Mapping m WHERE m.WraId = WeighingRecordAttachments.Id)
WHERE WeighingRecordId = :NewWeighingId
  AND EXISTS (SELECT 1 FROM Mapping m WHERE m.WraId = WeighingRecordAttachments.Id);

-- 6) 把现在属于 NewWeighingId 的 AttachmentFiles 的 AttachType 设置为 2
UPDATE AttachmentFiles
SET AttachType = 2
WHERE Id IN (
    SELECT DISTINCT AttachmentFileId FROM WeighingRecordAttachments WHERE WeighingRecordId = :NewWeighingId
);

COMMIT;