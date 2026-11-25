-- 0012 — horários fixos dos eventos do servidor (Bless Castle, SoD, Hellsgate...) para o banner da home,
-- com contagem regressiva. Guardado em dbo.SiteSetting com a chave 'schedule' (JSON validado pela API).
ALTER TABLE dbo.SiteSetting DROP CONSTRAINT CK_SiteSetting_Key;
ALTER TABLE dbo.SiteSetting ADD CONSTRAINT CK_SiteSetting_Key CHECK ([Key] IN ('server','downloads','drops','rareitems','schedule'));
