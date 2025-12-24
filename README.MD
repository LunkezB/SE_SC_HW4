# КПО. ДЗ-3

Работа выполнена Положенко Виталием Дмитриевичем, БПИ-247.

Система реализована в виде набора микросервисов:

- **API Gateway**
    - Принимает HTTP-запросы от клиента
    - Выполняет только роутинг запросов
    - Не содержит бизнес-логики

- **Orders Service**
    - Создание заказов
    - Просмотр списка заказов
    - Просмотр статуса заказа
    - Инициирует процесс оплаты заказа асинхронно

- **Payments Service**
    - Создание счёта пользователя (не более одного на user_id)
    - Пополнение счёта
    - Просмотр баланса
    - Асинхронная обработка списаний по заказам

- **Брокер сообщений (Kafka)**
    - Используется для асинхронного взаимодействия между Orders Service и Payments Service

## Запуск проекта

### Клонирование проекта
```bash
git clone https://github.com/LunkezB/SE_SC_HW4.git
cd SE_SC_HW4
```

### Переменные окружения (`.env`)
```env
POSTGRES_USER=postgres
POSTGRES_PASSWORD=change_me
DB_ACCOUNT=db_account
DB_ORDER=db_order
````

### Запуск

```bash
docker compose up --build
```

---

## Примеры curl-запросов

### Создание счёта

```bash
curl -X POST http://localhost:8080/accounts/create \
  -H "Content-Type: application/json" \
  -H "X-User-Id: <USER_ID>"
```

### Пополнение счёта

```bash
curl -X POST http://localhost:8080/accounts/top_up \
  -H "Content-Type: application/json" \
  -H "X-User-Id: <USER_ID>" \
  -d '{"amount": 1000}'
```

### Создание заказа

```bash
curl -X POST http://localhost:8080/orders/create \
  -H "Content-Type: application/json" \
  -H "X-User-Id: <USER_ID>" \
  -d '{"amount": 250, "description": "test order"}'
```

### Проверка статуса заказа

```bash
curl http://localhost:8080/orders/<ORDER_ID>/status \
  -H "X-User-Id: <USER_ID>"
```

---

## Swagger

Swagger UI доступен для каждого сервиса:

* **API Gateway**
  [http://localhost:8080/swagger](http://localhost:8080/swagger)

* **Orders Service**
  [http://localhost:8081/swagger](http://localhost:8081/swagger)

* **Payments Service**
  [http://localhost:8082/swagger](http://localhost:8082/swagger)