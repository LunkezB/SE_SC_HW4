CREATE
EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS orders
(
    id
    UUID
    PRIMARY
    KEY,
    user_id
    UUID
    NOT
    NULL,
    amount
    NUMERIC
(
    18,
    2
) NOT NULL,
    description TEXT NULL,
    status TEXT NOT NULL,
    created_at TIMESTAMPTZ NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL
    );

CREATE INDEX IF NOT EXISTS idx_orders_user_created ON orders (user_id, created_at DESC);

CREATE TABLE IF NOT EXISTS outbox_messages
(
    id
    BIGSERIAL
    PRIMARY
    KEY,
    event_id
    UUID
    NOT
    NULL
    UNIQUE,
    aggregate_id
    UUID
    NOT
    NULL,
    event_type
    TEXT
    NOT
    NULL,
    payload
    JSONB
    NOT
    NULL,
    created_at
    TIMESTAMPTZ
    NOT
    NULL
    DEFAULT
    NOW
(
),
    dispatched_at TIMESTAMPTZ NULL
    );

CREATE UNIQUE INDEX IF NOT EXISTS uq_outbox_aggregate_event ON outbox_messages (aggregate_id, event_type);
