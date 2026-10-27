SELECT t.table_id AS tabela_id,
       USER_NAME(t.creator) AS esquema,
       t.table_name AS tabela,
       t.table_type AS tipo,
       COALESCE(t.remarks, '') AS descricao
FROM SYS.SYSTABLE AS t
WHERE USER_NAME(t.creator) = ?
ORDER BY t.table_name
