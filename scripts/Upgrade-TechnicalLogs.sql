-- Отдельно выполнить на БД технических логов после Upgrade-Logs.sql перед запуском новой версии.
-- Не применять к основной БД. Нужны резервная копия, проверка места и согласованное окно обслуживания.
-- Добавление nullable-полей сохраняет исторические строки. В них новые поля останутся NULL.
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS correlation_id text;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS source_context text;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS diagnostic_session_id uuid;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS run_id text;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS fingerprint text;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS application_version text;
ALTER TABLE public.logs ADD COLUMN IF NOT EXISTS instance_id text;
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_logs_correlation_time ON public.logs (correlation_id, timestamp DESC, id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_logs_session_time ON public.logs (diagnostic_session_id, timestamp DESC, id);
CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_logs_errors_time ON public.logs (timestamp DESC, fingerprint) WHERE level IN ('Error','Fatal');
