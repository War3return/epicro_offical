# Railway 배포 가이드

## 사전 준비: DynamoDB 테이블 생성 (AWS)

AWS 콘솔 → DynamoDB → 테이블 생성

- 테이블 이름: `epicro_telegram_users`
- 파티션 키: `chat_id` (문자열)
- 나머지 기본값 → 생성

AWS IAM → 사용자 → 액세스 키 생성 (Railway에서 사용할 자격증명)

정책 추가:
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


## Railway 배포

### 1. 프로젝트 생성

[railway.app](https://railway.app) → New Project → Deploy from GitHub repo

`telegram-bot/` 폴더가 있는 레포 선택

> Railway는 루트에서 `requirements.txt`를 자동 감지하므로
> **Root Directory** 설정에서 `telegram-bot`으로 지정

### 2. 환경 변수 설정

Railway → Variables 탭에서 아래 추가:

| 키 | 값 |
|---|---|
| `BOT_TOKEN` | BotFather에서 받은 봇 토큰 |
| `ADMIN_CHAT_ID` | 관리자 Chat ID |
| `TABLE_NAME` | `epicro_telegram_users` |
| `AWS_ACCESS_KEY_ID` | IAM 액세스 키 |
| `AWS_SECRET_ACCESS_KEY` | IAM 시크릿 키 |
| `AWS_DEFAULT_REGION` | `ap-northeast-2` (서울) |

### 3. Start Command 설정

Railway → Settings → Deploy → Start Command:

```
python bot.py
```

또는 `Procfile`이 있으면 자동 감지됨.

### 4. Deploy

Variables 저장 후 자동으로 배포 시작.
Logs 탭에서 `[Bot] 시작 (polling 방식)` 메시지 확인.


## 봇 명령어 (BotFather 등록)

BotFather → /setcommands:

```
start - 알림 등록
stop - 알림 해제
chatid - 내 Chat ID 확인
help - 명령어 목록
```

관리자 전용 (등록 불필요):
- `/users` - 등록된 사용자 목록
- `/broadcast 메시지` - 전체 전송


## Lambda와 차이점

| | Lambda | Railway |
|---|---|---|
| 방식 | Webhook | Polling |
| API Gateway | 필요 | 불필요 |
| Webhook 등록 | 필요 | 불필요 |
| 실행 | 요청 시만 | 24/7 상시 |
| 비용 | 요청당 | 월정액 (무료 플랜 있음) |
