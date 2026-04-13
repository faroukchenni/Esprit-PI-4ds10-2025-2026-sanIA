"""User Pydantic schemas."""
from typing import Optional
from uuid import UUID
from datetime import datetime
from pydantic import BaseModel, EmailStr


class UserBase(BaseModel):
    email: str
    name:  str
    role:  str = "FARMER"


class UserCreate(UserBase):
    password:       str
    cooperative_id: Optional[str] = None
    farm_id:        Optional[str] = None


class UserUpdate(BaseModel):
    name:     Optional[str] = None
    email:    Optional[str] = None
    password: Optional[str] = None
    role:     Optional[str] = None


class User(UserBase):
    id:         str
    is_active:  bool = True
    created_at: Optional[datetime] = None

    class Config:
        from_attributes = True


class Token(BaseModel):
    access_token: str
    token_type:   str = "bearer"


class TokenPayload(BaseModel):
    sub: Optional[str] = None
