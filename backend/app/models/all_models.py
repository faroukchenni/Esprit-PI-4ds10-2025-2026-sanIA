"""
SanIA — SQLAlchemy ORM models.
UUID primary keys work with both PostgreSQL (native UUID) and SQLite (String).
"""
import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Boolean, Column, DateTime, Enum, Float,
    ForeignKey, Integer, String, Text,
)
from sqlalchemy.orm import relationship

from app.db.session import Base


def _uuid_col(primary_key=False, fk=None):
    """UUID column that works on both PostgreSQL and SQLite."""
    if fk:
        return Column(String(36), ForeignKey(fk), nullable=True)
    return Column(String(36), primary_key=primary_key, default=lambda: str(uuid.uuid4()))


# ── Enums ──────────────────────────────────────────────────────────────────────

class UserRole(str, enum.Enum):
    FARMER            = "FARMER"
    COOPERATIVE_ADMIN = "COOPERATIVE_ADMIN"


# ── Models ─────────────────────────────────────────────────────────────────────

class Cooperative(Base):
    __tablename__ = "cooperatives"
    id       = _uuid_col(primary_key=True)
    name     = Column(String(200), nullable=False)
    location = Column(String(200), nullable=True)
    users    = relationship("User", back_populates="cooperative")
    farms    = relationship("Farm", back_populates="cooperative")


class Farm(Base):
    __tablename__ = "farms"
    id             = _uuid_col(primary_key=True)
    cooperative_id = _uuid_col(fk="cooperatives.id")
    name           = Column(String(200), nullable=False)
    location       = Column(String(200), nullable=True)
    owner_name     = Column(String(200), nullable=True)
    cooperative    = relationship("Cooperative", back_populates="farms")


class User(Base):
    __tablename__ = "users"
    id             = _uuid_col(primary_key=True)
    name           = Column(String(200), nullable=False)
    email          = Column(String(200), unique=True, nullable=False, index=True)
    password_hash  = Column(String(300), nullable=False)
    role           = Column(Enum(UserRole), default=UserRole.FARMER, nullable=False)
    cooperative_id = _uuid_col(fk="cooperatives.id")
    farm_id        = Column(String(36), nullable=True)
    is_active      = Column(Boolean, default=True)
    created_at     = Column(DateTime, default=datetime.utcnow)
    cooperative    = relationship("Cooperative", back_populates="users")


class Field(Base):
    __tablename__ = "fields"
    id         = _uuid_col(primary_key=True)
    name       = Column(String(200), nullable=False)
    crop_type  = Column(String(100), nullable=True)
    area_ha    = Column(Float, nullable=True)
    polygon    = Column(Text, nullable=True)   # JSON string
    farm_id    = _uuid_col(fk="farms.id")


class SensorReading(Base):
    __tablename__ = "sensor_readings"
    id              = _uuid_col(primary_key=True)
    field_id        = _uuid_col(fk="fields.id")
    captured_at     = Column(DateTime, default=datetime.utcnow)
    temperature_C   = Column(Float, nullable=True)
    humidity_pct    = Column(Float, nullable=True)
    soil_moisture   = Column(Float, nullable=True)
    rain_mm         = Column(Float, nullable=True)


class IrrigationLog(Base):
    __tablename__ = "irrigation_logs"
    id             = _uuid_col(primary_key=True)
    field_id       = _uuid_col(fk="fields.id")
    decision_label = Column(String(20), nullable=False)
    confidence     = Column(Float, nullable=True)
    volume_m3      = Column(Float, nullable=True)
    created_at     = Column(DateTime, default=datetime.utcnow)


class DiseaseScan(Base):
    __tablename__ = "disease_scans"
    id         = _uuid_col(primary_key=True)
    field_id   = _uuid_col(fk="fields.id")
    disease    = Column(String(200), nullable=True)
    confidence = Column(Float, nullable=True)
    image_url  = Column(String(500), nullable=True)
    created_at = Column(DateTime, default=datetime.utcnow)


class NDVIRecord(Base):
    __tablename__ = "ndvi_records"
    id         = _uuid_col(primary_key=True)
    field_id   = _uuid_col(fk="fields.id")
    ndvi_value = Column(Float, nullable=True)
    captured_at = Column(DateTime, default=datetime.utcnow)


class Animal(Base):
    __tablename__ = "animals"
    id         = _uuid_col(primary_key=True)
    farm_id    = _uuid_col(fk="farms.id")
    breed      = Column(String(100), nullable=True)
    birth_date = Column(DateTime, nullable=True)
    species    = Column(String(100), nullable=True)
    vaccinations = relationship("VaccinationLog", back_populates="animal")
    treatments   = relationship("TreatmentLog",   back_populates="animal")


class VaccinationLog(Base):
    __tablename__ = "vaccination_logs"
    id          = _uuid_col(primary_key=True)
    animal_id   = _uuid_col(fk="animals.id")
    vaccine     = Column(String(200), nullable=True)
    given_at    = Column(DateTime, default=datetime.utcnow)
    animal      = relationship("Animal", back_populates="vaccinations")


class TreatmentLog(Base):
    __tablename__ = "treatment_logs"
    id          = _uuid_col(primary_key=True)
    animal_id   = _uuid_col(fk="animals.id")
    treatment   = Column(String(200), nullable=True)
    given_at    = Column(DateTime, default=datetime.utcnow)
    animal      = relationship("Animal", back_populates="treatments")


class Alert(Base):
    __tablename__ = "alerts"
    id         = _uuid_col(primary_key=True)
    field_id   = _uuid_col(fk="fields.id")
    message    = Column(Text, nullable=True)
    severity   = Column(String(20), default="info")
    created_at = Column(DateTime, default=datetime.utcnow)
