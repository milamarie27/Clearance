-- ============================================================
-- Online Clearance System — Full Database Schema
-- Run this script once on your MySQL database.
-- ============================================================

-- ── USERS (auth + profile) ───────────────────────────────
CREATE TABLE IF NOT EXISTS users (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    first_name     VARCHAR(50)  NOT NULL DEFAULT '',
    middle_initial VARCHAR(10)           DEFAULT '',
    last_name      VARCHAR(50)  NOT NULL DEFAULT '',
    suffix_name    VARCHAR(20)           DEFAULT '',
    email          VARCHAR(100) NOT NULL UNIQUE,
    password       VARCHAR(255) NOT NULL,
    id_number      VARCHAR(50)           DEFAULT NULL,
    role           VARCHAR(20)  NOT NULL DEFAULT 'Pending',
    is_active      TINYINT(1)   NOT NULL DEFAULT 0,
    created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- ── COURSES ──────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS courses (
    id          INT          AUTO_INCREMENT PRIMARY KEY,
    course_code VARCHAR(20)  NOT NULL,
    description VARCHAR(255)          DEFAULT ''
);

-- ── CURRICULUM (year + section per course) ───────────────
CREATE TABLE IF NOT EXISTS curriculum (
    id          INT         AUTO_INCREMENT PRIMARY KEY,
    course_id   INT         NOT NULL,
    year_level  INT         NOT NULL DEFAULT 1,
    section     VARCHAR(20) NOT NULL DEFAULT '',
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE
);

-- ── SECTIONS (admin-managed section list) ────────────────
CREATE TABLE IF NOT EXISTS sections (
    id           INT         AUTO_INCREMENT PRIMARY KEY,
    course_id    INT         NOT NULL,
    section_name VARCHAR(50) NOT NULL,
    year_level   VARCHAR(20)          DEFAULT '',
    FOREIGN KEY (course_id) REFERENCES courses(id) ON DELETE CASCADE
);

-- ── STUDENTS (links a user account to a student record) ──
CREATE TABLE IF NOT EXISTS students (
    id            INT         AUTO_INCREMENT PRIMARY KEY,
    user_id       INT         NOT NULL UNIQUE,
    student_number VARCHAR(50)         DEFAULT NULL,
    curriculum_id  INT                 DEFAULT NULL,
    FOREIGN KEY (user_id)      REFERENCES users(id)      ON DELETE CASCADE,
    FOREIGN KEY (curriculum_id) REFERENCES curriculum(id) ON DELETE SET NULL
);

-- ── SIGNATORIES (instructor employee IDs) ────────────────
CREATE TABLE IF NOT EXISTS signatories (
    id             INT         AUTO_INCREMENT PRIMARY KEY,
    user_id        INT         NOT NULL,
    signature_data MEDIUMTEXT             DEFAULT NULL,
    UNIQUE KEY uq_sig_user (user_id),
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- ── STUDENT SIGNATORIES (students assigned org positions) ─
CREATE TABLE IF NOT EXISTS student_signatories (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    user_id        INT          NOT NULL,
    position       VARCHAR(100)           DEFAULT '',
    signature_data MEDIUMTEXT             DEFAULT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- ── ACADEMIC PERIODS ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS academic_periods (
    id            INT         AUTO_INCREMENT PRIMARY KEY,
    academic_year VARCHAR(20) NOT NULL,
    semester      VARCHAR(30) NOT NULL,
    is_active     TINYINT(1)  NOT NULL DEFAULT 0
);

-- ── SUBJECTS ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS subjects (
    id           INT          AUTO_INCREMENT PRIMARY KEY,
    mis_code     VARCHAR(50)           DEFAULT '',
    subject_code VARCHAR(50)  NOT NULL UNIQUE,
    title        VARCHAR(255)          DEFAULT '',
    lec_units    INT          NOT NULL DEFAULT 0,
    lab_units    INT          NOT NULL DEFAULT 0
);

-- ── SUBJECT OFFERINGS ────────────────────────────────────
CREATE TABLE IF NOT EXISTS subject_offerings (
    id           INT         AUTO_INCREMENT PRIMARY KEY,
    mis_code     VARCHAR(50) NOT NULL UNIQUE,
    subject_code VARCHAR(50) NOT NULL,
    instructor_id VARCHAR(50) NOT NULL,
    period_id    INT         NOT NULL,
    is_active    TINYINT(1)  NOT NULL DEFAULT 1,
    FOREIGN KEY (period_id) REFERENCES academic_periods(id) ON DELETE CASCADE
);

-- ── STATUS TABLE (1=Pending, 2=Cleared) ──────────────────
CREATE TABLE IF NOT EXISTS status_table (
    id    INT         AUTO_INCREMENT PRIMARY KEY,
    label VARCHAR(50) NOT NULL
);

INSERT IGNORE INTO status_table (id, label) VALUES (1, 'Pending'), (2, 'Cleared');

-- ── CLEARANCE SUBJECTS (student enrollment + clearance) ──
CREATE TABLE IF NOT EXISTS clearance_subjects (
    id             INT         AUTO_INCREMENT PRIMARY KEY,
    student_number VARCHAR(50) NOT NULL,
    mis_code       VARCHAR(50) NOT NULL,
    status         INT         NOT NULL DEFAULT 1,
    period_id      INT         NOT NULL,
    UNIQUE KEY uq_cs (student_number, mis_code),
    FOREIGN KEY (status)    REFERENCES status_table(id),
    FOREIGN KEY (period_id) REFERENCES academic_periods(id) ON DELETE CASCADE
);

-- ── ORGANIZATIONS ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS organizations (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    org_name       VARCHAR(100) NOT NULL,
    org_signatory  VARCHAR(100)          DEFAULT '',
    position_title VARCHAR(100)          DEFAULT '',
    curriculum_id  INT                   DEFAULT NULL,
    FOREIGN KEY (curriculum_id) REFERENCES curriculum(id) ON DELETE SET NULL
);

-- ── CLEARANCE ORGANIZATION ───────────────────────────────
CREATE TABLE IF NOT EXISTS clearance_organization (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    student_number VARCHAR(50)  NOT NULL,
    org_name       VARCHAR(100) NOT NULL,
    status         INT          NOT NULL DEFAULT 1,
    UNIQUE KEY uq_co (student_number, org_name),
    FOREIGN KEY (status) REFERENCES status_table(id)
);

-- ── ANNOUNCEMENTS ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS announcements (
    id         INT          AUTO_INCREMENT PRIMARY KEY,
    title      VARCHAR(255) NOT NULL,
    content    TEXT,
    type       VARCHAR(50)           DEFAULT 'General',
    author_id  INT                   DEFAULT NULL,
    created_at DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (author_id) REFERENCES users(id) ON DELETE SET NULL
);

-- ============================================================
-- SAMPLE DATA — uncomment to seed an admin account
-- Password hash below is BCrypt for "Admin@123"
-- ============================================================
-- INSERT IGNORE INTO users (first_name, last_name, username, password, role, is_active)
-- VALUES ('Admin', 'User', 'admin', '$2a$11$xxxYOURHASHxxx', 'Admin', 1);
