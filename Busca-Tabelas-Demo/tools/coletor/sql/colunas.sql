SELECT t.table_id AS tabela_id,
       USER_NAME(t.creator) AS esquema,
       t.table_name AS tabela,
       c.column_id AS coluna_id,
       c.column_name AS coluna,
       d.domain_name AS tipo,
       c.width AS tamanho_ou_precisao,
       c.scale AS escala,
       c.pkey AS chave_primaria,
       c.nulls AS aceita_nulo,
       c."default" AS valor_padrao,
       COALESCE(c.remarks, '') AS descricao
FROM SYS.SYSTABLE AS t
JOIN SYS.SYSCOLUMN AS c ON c.table_id = t.table_id
JOIN SYS.SYSDOMAIN AS d ON d.domain_id = c.domain_id
WHERE USER_NAME(t.creator) = ?
ORDER BY t.table_name, c.column_id
