CREATE
EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS accounts
(
    account_id
    UUID
    PRIMARY
    KEY,
    user_id
    UUID
    NOT
    NULL
    UNIQUE,
    balance
    NUMERIC
(
    18,
    2
) NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW
(
)
    );

CREATE TABLE IF NOT EXISTS inbox_messages
(
    message_id
    UUID
    PRIMARY
    KEY,
    payload
    JSONB
    NOT
    NULL,
    received_at
    TIMESTAMPTZ
    NOT
    NULL
    DEFAULT
    NOW
(
)
    );

CREATE TABLE IF NOT EXISTS charges
(
    order_id
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
    status TEXT NOT NULL,
    reason TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW
(
),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW
(
)
    );

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
