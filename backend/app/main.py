"""
SanIA AgriSmart — FastAPI Application
======================================
All routers mounted under /api/v1.
"""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware

from app.core.config import settings
from app.db.session import Base, engine
from app.routers import auth, irrigation


# ── DB init (create tables if they don't exist) ───────────────────────────────
def _init_db():
    try:
        Base.metadata.create_all(bind=engine)
    except Exception as exc:
        print(f"[WARN] Could not create DB tables: {exc}")


# ── Irrigation agent singleton ────────────────────────────────────────────────
def _init_irrigation_agent(app: FastAPI):
    try:
        from app.services.irrigation_agent import IrrigationAgentService
        app.state.irrigation_agent = IrrigationAgentService()
        ready = app.state.irrigation_agent.is_ready
        print(f"[OK] Irrigation agent loaded | model_ready={ready}")
    except Exception as exc:
        print(f"[WARN] Irrigation agent failed to load: {exc}")
        app.state.irrigation_agent = None


# ── Lifespan ──────────────────────────────────────────────────────────────────
@asynccontextmanager
async def lifespan(app: FastAPI):
    _init_db()
    _init_irrigation_agent(app)
    yield


# ── App ────────────────────────────────────────────────────────────────────────
app = FastAPI(
    title=settings.PROJECT_NAME,
    version=settings.VERSION,
    lifespan=lifespan,
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ── Routers ───────────────────────────────────────────────────────────────────
app.include_router(auth.router,       prefix=settings.API_V1_STR, tags=["Auth"])
app.include_router(irrigation.router, prefix=settings.API_V1_STR, tags=["Irrigation"])


@app.get("/")
def root():
    return {"message": f"Welcome to {settings.PROJECT_NAME}"}
