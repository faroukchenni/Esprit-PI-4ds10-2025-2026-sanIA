"""Authentication router: register, login, /me."""
from fastapi import APIRouter, Depends, HTTPException, status
from fastapi.security import OAuth2PasswordRequestForm
from sqlalchemy.orm import Session

from app.core.security import get_password_hash, verify_password, create_access_token
from app.db.session import get_db
from app.models.all_models import User, UserRole
from app.routers.deps import get_current_active_user
from app.schemas.user import UserCreate, Token
import uuid

router = APIRouter(prefix="/auth", tags=["Auth"])


@router.post("/register", status_code=status.HTTP_201_CREATED)
def register_user(user_in: UserCreate, db: Session = Depends(get_db)):
    existing = db.query(User).filter(User.email == user_in.email).first()
    if existing:
        raise HTTPException(status_code=400, detail="User already exists")
    user = User(
        id            = str(uuid.uuid4()),
        name          = user_in.name,
        email         = user_in.email,
        password_hash = get_password_hash(user_in.password),
        role          = UserRole(user_in.role) if user_in.role else UserRole.FARMER,
        cooperative_id = user_in.cooperative_id,
        farm_id       = user_in.farm_id,
    )
    db.add(user)
    db.commit()
    db.refresh(user)
    return {"id": user.id, "email": user.email, "name": user.name}


@router.post("/login", response_model=Token)
def login(form_data: OAuth2PasswordRequestForm = Depends(), db: Session = Depends(get_db)):
    user = db.query(User).filter(User.email == form_data.username).first()
    if not user or not verify_password(form_data.password, user.password_hash):
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Incorrect email or password",
            headers={"WWW-Authenticate": "Bearer"},
        )
    token = create_access_token(data={"sub": str(user.id)})
    return Token(access_token=token, token_type="bearer")


@router.get("/me")
def read_users_me(current_user: User = Depends(get_current_active_user)):
    return {
        "id":    current_user.id,
        "email": current_user.email,
        "name":  current_user.name,
        "role":  current_user.role,
    }
