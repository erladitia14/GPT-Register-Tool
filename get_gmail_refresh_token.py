"""Google OAuth helper: get a refresh_token for Gmail IMAP/SMTP access.

Works for any Google account incl. G Suite (custom domain like @bekri.id),
because Gmail's IMAP/SMTP servers (imap.gmail.com / smtp.gmail.com) serve all
Google Workspace mailboxes — OAuth scopes are identical.

Flow:
  1. Create OAuth client (see steps printed below)
  2. Run:  python get_gmail_refresh_token.py <client_id> <client_secret>
  3. A browser opens -> log in with the target account -> Allow
  4. Copy the 'code=' from the broken-looking redirect URL
  5. Paste it here -> you get the refresh_token + ready-to-use import line
"""
import sys
import webbrowser
import requests

SCOPE = "https://mail.google.com/"
REDIRECT = "http://localhost:8085/callback"
AUTH_URL = "https://accounts.google.com/o/oauth2/v2/auth"
TOKEN_URL = "https://oauth2.googleapis.com/token"


def main():
    if len(sys.argv) != 3:
        print("Usage: python get_gmail_refresh_token.py <client_id> <client_secret>")
        sys.exit(1)
    client_id, client_secret = sys.argv[1], sys.argv[2]

    auth_url = (
        f"{AUTH_URL}?client_id={client_id}"
        f"&redirect_uri={REDIRECT}"
        f"&response_type=code&scope={SCOPE}"
        "&access_type=offline&prompt=consent"
    )
    print("\n[1] Opening browser... log in with the TARGET account and click Allow.")
    print("    After Allow, the browser lands on a 'this site can't be reached' page.")
    print("    That is EXPECTED. Copy the FULL address bar URL (it contains code=...).")
    print(f"\n    URL: {auth_url}\n")
    webbrowser.open(auth_url)

    url = input("[2] Paste the full redirect URL here: ").strip()
    if "code=" not in url:
        print("[!] No code= found in that URL. Aborting.")
        sys.exit(1)
    code = url.split("code=")[1].split("&")[0]

    resp = requests.post(
        TOKEN_URL,
        data={
            "code": code,
            "client_id": client_id,
            "client_secret": client_secret,
            "redirect_uri": REDIRECT,
            "grant_type": "authorization_code",
        },
        timeout=30,
    )
    body = resp.json()
    if "refresh_token" not in body:
        print(f"[!] Token exchange failed: {body}")
        print("    Common causes: consent not granted, wrong client, or code already used.")
        sys.exit(1)

    rt = body["refresh_token"]
    email = input("[3] Enter the account email (for the import line): ").strip()
    print("\n=== SUCCESS ===")
    print(f"refresh_token: {rt}")
    print("\n=== Paste this line into chatai_mailbox.txt ===")
    print(f"gmail://{email}----{client_id}----{client_secret}----{rt}")


if __name__ == "__main__":
    main()
