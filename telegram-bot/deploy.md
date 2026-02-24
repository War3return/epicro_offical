# Lambda 봇 배포 가이드

## 1. DynamoDB 테이블 생성

AWS 콘솔 → DynamoDB → 테이블 생성

- 테이블 이름: `epicro_telegram_users`
- 파티션 키: `chat_id` (문자열)
- 나머지 기본값 유지 → 생성


## 2. Lambda 함수 생성

AWS 콘솔 → Lambda → 함수 생성

- 함수 이름: `epicro-telegram-bot`
- 런타임: Python 3.12
- 아키텍처: x86_64

### 코드 업로드
Lambda 콘솔 코드 편집기에 `bot.py` 내용을 붙여넣기 → Deploy


### 환경 변수 설정
Lambda → 구성 → 환경 변수

| 키 | 값 |
|---|---|
| `BOT_TOKEN` | BotFather에서 받은 봇 토큰 |
| `ADMIN_CHAT_ID` | 관리자 본인 Chat ID |
| `TABLE_NAME` | `epicro_telegram_users` |


### IAM 권한 추가
Lambda → 구성 → 권한 → 실행 역할 클릭

역할에 인라인 정책 추가:
```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:PutItem",
        "dynamodb:GetItem",
        "dynamodb:DeleteItem",
        "dynamodb:Scan"
      ],
      "Resource": "arn:aws:dynamodb:*:*:table/epicro_telegram_users"
    }
  ]
}
```

### 타임아웃 설정
Lambda → 구성 → 일반 구성 → 편집
- 타임아웃: 30초 (broadcast 시 시간 필요)


## 3. API Gateway 생성

AWS 콘솔 → API Gateway → API 생성

- HTTP API 선택 → 빌드
- 통합: Lambda → epicro-telegram-bot 선택
- 라우트: `POST /webhook`
- 배포 → 스테이지 이름: `prod`

생성 후 **호출 URL** 복사 (예: `https://abc123.execute-api.ap-northeast-2.amazonaws.com/prod`)


## 4. Telegram Webhook 등록

아래 URL을 브라우저에서 열거나 curl로 실행:

```
https://api.telegram.org/bot{봇토큰}/setWebhook?url=https://{API게이트웨이주소}/prod/webhook
```

성공 시 응답:
```json
{"ok": true, "result": true, "description": "Webhook was set"}
```

## 5. 에피크로 설정

에피크로는 알림을 Telegram API에 직접 전송하므로 별도 변경 없음.
사용자가 `/start` 또는 `/chatid` 로 Chat ID를 확인 후 에피크로에 입력.


## 봇 명령어 (BotFather 등록)

BotFather → /setcommands 에서 아래 입력:

```
start - 알림 등록
stop - 알림 해제
chatid - 내 Chat ID 확인
help - 명령어 목록
```

관리자 전용 (BotFather에 등록 안 해도 됨):
- `/users` - 등록된 사용자 목록
- `/broadcast 메시지` - 전체 메시지 전송
