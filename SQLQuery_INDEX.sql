-- Timestamp用インデックス
CREATE INDEX IX_HttpLogs_Timestamp ON HttpLogs(Timestamp);

-- StatusCode用インデックス
CREATE INDEX IX_HttpLogs_StatusCode ON HttpLogs(StatusCode);

-- 現在のユーザーの権限確認
SELECT 
    dp.name AS principal_name,
    dp.type_desc AS principal_type,
    o.name AS object_name,
    p.permission_name,
    p.state_desc AS permission_state
FROM sys.database_permissions p
JOIN sys.objects o ON p.major_id = o.object_id
JOIN sys.database_principals dp ON p.grantee_principal_id = dp.principal_id
WHERE o.name = 'HttpLogs';


-- テストデータ挿入
INSERT INTO HttpLogs (
    Timestamp, 
    IpAddress, 
    Method, 
    Url, 
    StatusCode, 
    BytesSent,
    Referer,
    UserAgent
) VALUES (
    GETUTCDATE(),
    '192.168.1.1',
    'GET',
    '/test',
    200,
    1024,
    'http://example.com',
    'Mozilla/5.0'
);

-- データ確認
SELECT TOP 1 * FROM HttpLogs ORDER BY Id DESC;