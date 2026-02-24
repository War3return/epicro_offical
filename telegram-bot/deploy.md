# Railway 배포 가이드

Railway는 클라우드 서버입니다.
봇 코드를 Railway 서버에 올리면 내 컴퓨터가 꺼져도 24시간 돌아갑니다.

---

## 1단계: Railway 가입 및 프로젝트 생성

1. [railway.app](https://railway.app) 접속 → GitHub 계정으로 로그인
2. **New Project** 클릭
3. **Deploy from GitHub repo** 선택 → 이 레포 선택
   - GitHub에 코드가 없다면 아래 "CLI로 올리기" 참고


## 2단계: PostgreSQL DB 추가

Railway 프로젝트 화면에서:

1. **+ New** 클릭 → **Database** → **PostgreSQL** 선택
2. 생성 완료 → PostgreSQL 클릭 → **Variables** 탭에서 `DATABASE_URL` 확인
   (Railway가 자동으로 만들어 줌, 직접 입력 불필요)


## 3단계: 봇 서비스 설정

Railway 프로젝트 → 봇 서비스 클릭 → **Settings** 탭

- **Root Directory**: `telegram-bot`
- **Start Command**: `python bot.py`

**Variables** 탭에서 아래 두 개만 추가:

| 키 | 값 |
|---|---|
| `BOT_TOKEN` | BotFather에서 받은 봇 토큰 |
| `ADMIN_CHAT_ID` | 관리자 본인 Chat ID |

> `DATABASE_URL`은 PostgreSQL 서비스와 연결하면 자동으로 들어옵니다.
> Railway 프로젝트 → 봇 서비스 → **Variables** → **Add Reference** →
> PostgreSQL의 `DATABASE_URL` 선택


## 4단계: 배포

Variables 저장 후 자동으로 배포됩니다.
**Deployments** 탭 → 최신 배포 클릭 → 로그에서 확인:

```
[DB] 테이블 준비 완료
[Bot] 시작 (polling)
```


---

## CLI로 올리기 (GitHub 없이)

Railway CLI를 사용하면 GitHub 없이 직접 올릴 수 있습니다.

```bash
# Railway CLI 설치
npm install -g @railway/cli

# 로그인
railway login

# telegram-bot 폴더에서 실행
cd telegram-bot
railway up
```


## 봇 명령어 (BotFather 등록)

BotFather → /setcommands:

```
start - 알림 등록
stop - 알림 해제
chatid - 내 Chat ID 확인
help - 명령어 목록
```

관리자 전용 (BotFather 등록 불필요):
- `/users` - 등록된 사용자 수와 목록
- `/broadcast 내용` - 전체 사용자에게 메시지 전송
