import json
import os
import time
import urllib.request
from datetime import datetime

import boto3

BOT_TOKEN = os.environ["BOT_TOKEN"]
ADMIN_CHAT_ID = int(os.environ["ADMIN_CHAT_ID"])
TABLE_NAME = os.environ.get("TABLE_NAME", "epicro_telegram_users")

dynamodb = boto3.resource("dynamodb")
table = dynamodb.Table(TABLE_NAME)


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


# ── DynamoDB ──────────────────────────────────────────────────────────────────

def register_user(chat_id, username):
    table.put_item(Item={
        "chat_id": str(chat_id),
        "username": username or "",
        "registered_at": datetime.utcnow().isoformat(),
    })

def unregister_user(chat_id):
    table.delete_item(Key={"chat_id": str(chat_id)})

def get_all_users():
    return table.scan().get("Items", [])


# ── 명령어 처리 ───────────────────────────────────────────────────────────────

def handle_command(chat_id, username, cmd, full_text):
    if cmd == "/start":
        register_user(chat_id, username)
        send_message(chat_id,
            f"✅ 알림이 등록되었습니다!\n\n"
            f"내 Chat ID: `{chat_id}`\n\n"
            f"에피크로 → 기타 탭 → 텔레그램 설정창에 이 번호를 입력하세요.",
            parse_mode="Markdown")

    elif cmd == "/stop":
        unregister_user(chat_id)
        send_message(chat_id, "🔕 등록이 해제되었습니다.")

    elif cmd == "/chatid":
        send_message(chat_id,
            f"내 Chat ID: `{chat_id}`\n\n"
            f"에피크로 → 기타 탭 → 텔레그램 설정창에 입력하세요.",
            parse_mode="Markdown")

    elif cmd == "/help":
        send_message(chat_id,
            "📋 명령어 목록\n"
            "/start - 알림 등록\n"
            "/stop  - 알림 해제\n"
            "/chatid - 내 Chat ID 확인\n"
            "/help  - 명령어 목록")

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
            name = f"@{u['username']}" if u.get("username") else u["chat_id"]
            lines.append(f"• {name}")
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
        if not users:
            send_message(chat_id, "등록된 사용자가 없습니다.")
            return
        success, fail = 0, 0
        for user in users:
            try:
                send_message(int(user["chat_id"]), f"📢 공지\n{msg}")
                success += 1
            except Exception:
                fail += 1
        send_message(chat_id, f"✅ 전송 완료\n성공: {success}명 / 실패: {fail}명")

    else:
        send_message(chat_id, "알 수 없는 관리자 명령어입니다.")


# ── Polling 루프 ──────────────────────────────────────────────────────────────

def main():
    print("[Bot] 시작 (polling 방식)")

    # 시작 시 쌓인 메시지 건너뜀
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
                    print(f"[Error] handle_command: {e}")
        except Exception as e:
            print(f"[Error] poll: {e}")
            time.sleep(5)


if __name__ == "__main__":
    main()
