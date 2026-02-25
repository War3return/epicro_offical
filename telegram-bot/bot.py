import json
import os
import time
import urllib.request

import psycopg2
import psycopg2.extras

BOT_TOKEN = os.environ["BOT_TOKEN"]
ADMIN_CHAT_ID = int(os.environ["ADMIN_CHAT_ID"])
DATABASE_URL = os.environ["DATABASE_URL"]


# ── DB 초기화 ─────────────────────────────────────────────────────────────────

def get_db():
    return psycopg2.connect(DATABASE_URL)

def init_db():
    with get_db() as conn:
        with conn.cursor() as cur:
            cur.execute("""
                CREATE TABLE IF NOT EXISTS users (
                    chat_id   BIGINT PRIMARY KEY,
                    username  TEXT,
                    joined_at TIMESTAMP DEFAULT NOW()
                )
            """)
        conn.commit()
    print("[DB] 테이블 준비 완료")


# ── DB 조작 ───────────────────────────────────────────────────────────────────

def register_user(chat_id, username):
    with get_db() as conn:
        with conn.cursor() as cur:
            cur.execute(
                "INSERT INTO users (chat_id, username) VALUES (%s, %s)"
                " ON CONFLICT (chat_id) DO NOTHING",
                (chat_id, username or "")
            )
        conn.commit()

def unregister_user(chat_id):
    with get_db() as conn:
        with conn.cursor() as cur:
            cur.execute("DELETE FROM users WHERE chat_id = %s", (chat_id,))
        conn.commit()

def get_all_users():
    with get_db() as conn:
        with conn.cursor(cursor_factory=psycopg2.extras.DictCursor) as cur:
            cur.execute("SELECT chat_id, username FROM users ORDER BY joined_at")
            return cur.fetchall()


# ── Telegram API ──────────────────────────────────────────────────────────────

def send_message(chat_id, text, parse_mode=None):
    url = f"https://api.telegram.org/bot{BOT_TOKEN}/sendMessage"
    payload = {"chat_id": chat_id, "text": text}
    if parse_mode:
        payload["parse_mode"] = parse_mode
    data = json.dumps(payload).encode()
    req = urllib.request.Request(
        url, data=data, headers={"Content-Type": "application/json"}
    )
    urllib.request.urlopen(req, timeout=10)

def get_updates(offset=None):
    url = f"https://api.telegram.org/bot{BOT_TOKEN}/getUpdates?timeout=30"
    if offset is not None:
        url += f"&offset={offset}"
    req = urllib.request.Request(url)
    resp = urllib.request.urlopen(req, timeout=35)
    return json.loads(resp.read())


# ── 명령어 처리 ───────────────────────────────────────────────────────────────

def handle_command(chat_id, username, cmd, full_text):
    if cmd == "/start":
        register_user(chat_id, username)
        send_message(chat_id,
            f"✅ 등록되었습니다!\n\n"
            f"내 Chat ID: `{chat_id}`\n\n"
            f"이 번호를 에피크로 → 기타 탭 → 텔레그램 설정에 입력하세요.",
            parse_mode="Markdown")

    elif cmd == "/stop":
        unregister_user(chat_id)
        send_message(chat_id, "🔕 등록이 해제되었습니다.")

    elif cmd == "/chatid":
        send_message(chat_id,
            f"내 Chat ID: `{chat_id}`\n\n"
            f"에피크로 → 기타 탭 → 텔레그램 설정에 입력하세요.",
            parse_mode="Markdown")

    elif cmd == "/help":
        send_message(chat_id,
            "📋 명령어 목록\n"
            "/start  - 등록 및 Chat ID 확인\n"
            "/stop   - 등록 해제\n"
            "/chatid - 내 Chat ID 확인\n"
            "/help   - 명령어 목록")

    elif chat_id == ADMIN_CHAT_ID:
        handle_admin_command(chat_id, cmd, full_text)


def handle_admin_command(chat_id, cmd, full_text):
    if cmd == "/users":
        users = get_all_users()
        count = len(users)
        if count == 0:
            send_message(chat_id, "등록된 사용자가 없습니다.")
            return
        lines = []
        for u in users[:30]:
            name = f"@{u['username']}" if u["username"] else str(u["chat_id"])
            lines.append(f"• {name} ({u['chat_id']})")
        text = f"👥 등록된 사용자: {count}명\n\n" + "\n".join(lines)
        if count > 30:
            text += f"\n... 외 {count - 30}명"
        send_message(chat_id, text)

    elif cmd == "/broadcast":
        msg = full_text[len("/broadcast"):].strip()
        if not msg:
            send_message(chat_id, "사용법: /broadcast 보낼내용")
            return
        users = get_all_users()
        success, fail = 0, 0
        for user in users:
            try:
                send_message(user["chat_id"], f"📢 공지\n{msg}")
                success += 1
            except Exception:
                fail += 1
        send_message(chat_id, f"✅ 전송 완료\n성공: {success}명 / 실패: {fail}명")

    else:
        send_message(chat_id, "알 수 없는 명령어입니다.")


# ── Polling 루프 ──────────────────────────────────────────────────────────────

def main():
    init_db()
    print("[Bot] 시작 (polling)")

    try:
        result = get_updates(offset=-1).get("result", [])
        offset = result[-1]["update_id"] + 1 if result else None
    except Exception:
        offset = None

    while True:
        try:
            data = get_updates(offset)
            for update in data.get("result", []):
                offset = update["update_id"] + 1
                message = update.get("message", {})
                if not message or not message.get("text"):
                    continue
                chat_id = message["chat"]["id"]
                username = message.get("from", {}).get("username", "")
                text = message["text"].strip()
                cmd = text.split()[0].lower()
                if "@" in cmd:
                    cmd = cmd[: cmd.index("@")]
                try:
                    handle_command(chat_id, username, cmd, text)
                except Exception as e:
                    print(f"[Error] handle: {e}")
        except Exception as e:
            print(f"[Error] poll: {e}")
            time.sleep(5)


if __name__ == "__main__":
    main()
