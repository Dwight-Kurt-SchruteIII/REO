const STORAGE_TOKEN_KEY = "realestate.jwt";
const STORAGE_USER_KEY = "realestate.user";

const dom = {
  form: document.getElementById("loginPageForm"),
  username: document.getElementById("username"),
  password: document.getElementById("password"),
  submit: document.getElementById("submitLogin"),
  notice: document.getElementById("notice")
};

bootstrap();

async function bootstrap() {
  const session = await fetch("/auth/me", {
    method: "GET",
    headers: {
      Accept: "application/json"
    }
  });

  if (session.ok) {
    window.location.replace("/index.html");
    return;
  }

  wireEvents();
}

function wireEvents() {
  dom.form.addEventListener("submit", async (event) => {
    event.preventDefault();

    const username = dom.username.value.trim();
    const password = dom.password.value;

    if (!username || !password) {
      notify("error", "Username and password are required.");
      return;
    }

    dom.submit.disabled = true;

    try {
      const response = await fetch("/auth/login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "application/json"
        },
        body: JSON.stringify({ username, password })
      });

      const data = await readResponseBody(response);

      if (!response.ok) {
        const detail = typeof data === "object" && data?.detail
          ? String(data.detail)
          : "Sign in failed.";
        notify("error", detail);
        return;
      }

      const token = data?.accessToken;
      const user = data?.user;

      if (typeof token === "string" && token.length > 0) {
        window.localStorage.setItem(STORAGE_TOKEN_KEY, token);
      }

      if (user && typeof user === "object") {
        window.localStorage.setItem(STORAGE_USER_KEY, JSON.stringify(user));
      }

      window.location.replace("/index.html");
    } catch {
      notify("error", "Network error. API is not reachable.");
    } finally {
      dom.submit.disabled = false;
    }
  });
}

async function readResponseBody(response) {
  const contentType = response.headers.get("content-type") || "";

  if (contentType.includes("application/json")) {
    try {
      return await response.json();
    } catch {
      return null;
    }
  }

  try {
    return await response.text();
  } catch {
    return null;
  }
}

function notify(type, message) {
  dom.notice.className = `notice ${type}`;
  dom.notice.textContent = message;
  dom.notice.classList.remove("hidden");
}
