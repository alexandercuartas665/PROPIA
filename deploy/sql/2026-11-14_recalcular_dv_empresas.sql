-- Recalcula el digito de verificacion (DIAN) de las empresas que lo tienen guardado.
-- Pesos de DERECHA a izquierda: 3,7,13,17,19,23,29,37,41,43,47,53,59,67,71; dv = r si r<=1, si no 11-r (r = suma mod 11).
with calc as (
  select e.id,
         (select sum(substr(e.nit, length(e.nit) - i, 1)::int * (array[3,7,13,17,19,23,29,37,41,43,47,53,59,67,71])[i + 1])
            from generate_series(0, length(e.nit) - 1) i) % 11 as r
  from empresas e
  where e.digito_verificacion is not null and e.nit ~ '^[0-9]{1,15}$'
)
update empresas e set digito_verificacion = case when c.r <= 1 then c.r else 11 - c.r end::text
from calc c
where e.id = c.id and e.digito_verificacion is distinct from (case when c.r <= 1 then c.r else 11 - c.r end::text);
