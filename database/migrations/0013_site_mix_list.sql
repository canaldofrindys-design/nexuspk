-- 0013 — Mix List pública (fórmulas de mix por equipamento), editada no painel em /admin/mix.
-- Guardada em dbo.SiteSetting com a chave 'mix' (JSON validado pela API).
ALTER TABLE dbo.SiteSetting DROP CONSTRAINT CK_SiteSetting_Key;
ALTER TABLE dbo.SiteSetting ADD CONSTRAINT CK_SiteSetting_Key CHECK ([Key] IN ('server','downloads','drops','rareitems','schedule','mix'));
