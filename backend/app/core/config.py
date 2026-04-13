"""Central configuration — reads from .env, falls back to defaults."""
import os
from pathlib import Path

_ENV_FILE = Path(__file__).resolve().parents[3] / ".env"

def _get(key: str, default: str = "") -> str:
    """Read from .env file first, fall back to os.environ, then default."""
    if _ENV_FILE.exists():
        for line in _ENV_FILE.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if line.startswith("#") or "=" not in line:
                continue
            k, _, v = line.partition("=")
            if k.strip() == key:
                return v.strip()
    return os.environ.get(key, default)


class Settings:
    PROJECT_NAME: str = _get("PROJECT_NAME", "Sania Smart Agriculture")
    VERSION:      str = _get("VERSION",      "1.0.0")
    API_V1_STR:   str = _get("API_V1_STR",   "/api/v1")

    SECRET_KEY:               str = _get("SECRET_KEY", "SUPER_SECRET_KEY_DONT_USE_IN_PROD")
    ALGORITHM:                str = _get("ALGORITHM",  "HS256")
    ACCESS_TOKEN_EXPIRE_MINUTES: int = int(_get("ACCESS_TOKEN_EXPIRE_MINUTES", "1440"))  # 24h

    # Database — defaults to SQLite for local demo; set DATABASE_URL in .env for PostgreSQL
    POSTGRES_USER:     str = _get("POSTGRES_USER",     "postgres")
    POSTGRES_PASSWORD: str = _get("POSTGRES_PASSWORD", "sania_pass")
    POSTGRES_SERVER:   str = _get("POSTGRES_SERVER",   "localhost")
    POSTGRES_PORT:     str = _get("POSTGRES_PORT",     "5432")
    POSTGRES_DB:       str = _get("POSTGRES_DB",       "sania_db")

    @property
    def DATABASE_URL(self) -> str:
        url_env = _get("DATABASE_URL", "")
        if url_env:
            return url_env
        # SQLite fallback (no PostgreSQL required for demo)
        sqlite_path = Path(__file__).resolve().parents[3] / "sania_test.db"
        return f"sqlite:///{sqlite_path}"

    OLLAMA_BASE_URL: str = _get("OLLAMA_BASE_URL", "http://localhost:11434")
    OLLAMA_MODEL:    str = _get("OLLAMA_MODEL",    "llama3")
    RAG_DATA_DIR:    str = _get("RAG_DATA_DIR",    "../Data/treatment2")


settings = Settings()
