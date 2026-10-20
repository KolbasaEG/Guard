-- Выполнить ОТДЕЛЬНО на БД технических логов перед выпуском. Нужна резервная копия.
-- ADD COLUMN с DEFAULT UUID переписывает существующие строки; запланировать окно по объёму таблицы.
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS id uuid DEFAULT gen_random_uuid();
ALTER TABLE public.logs ALTER COLUMN id SET NOT NULL;
CREATE UNIQUE INDEX CONCURRENTLY IF NOT EXISTS ux_logs_id ON public.logs (id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_logs_timestamp_id ON public.logs (timestamp DESC, id);
