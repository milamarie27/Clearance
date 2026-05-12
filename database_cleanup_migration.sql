-- ============================================================
-- Database Cleanup Migration Script
-- Run this script to restructure the database to match the cleaned models
-- ============================================================

-- Step 1: Backup existing data (optional but recommended)
-- CREATE TABLE users_backup AS SELECT * FROM users;
-- CREATE TABLE students_backup AS SELECT * FROM students;

-- Step 2: Update Student table to match new structure
-- Remove duplicate user fields from students table
ALTER TABLE students 
DROP COLUMN IF EXISTS first_name,
DROP COLUMN IF EXISTS middle_initial,
DROP COLUMN IF EXISTS last_name,
DROP COLUMN IF EXISTS suffix,
DROP COLUMN IF EXISTS student_id,
DROP COLUMN IF EXISTS password_hash,
DROP COLUMN IF EXISTS course_id,
DROP COLUMN IF EXISTS year_level,
DROP COLUMN IF EXISTS section,
DROP COLUMN IF EXISTS status,
DROP COLUMN IF EXISTS additional_role,
DROP COLUMN IF EXISTS is_active;

-- Add missing columns if they don't exist
ALTER TABLE students 
ADD COLUMN IF NOT EXISTS user_id INT NOT NULL AFTER id,
ADD COLUMN IF NOT EXISTS student_number VARCHAR(50) DEFAULT NULL AFTER user_id,
ADD COLUMN IF NOT EXISTS curriculum_id INT DEFAULT NULL AFTER student_number;

-- Add foreign key constraint for user_id
ALTER TABLE students 
ADD CONSTRAINT fk_students_user_id 
FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE;

-- Add foreign key constraint for curriculum_id
ALTER TABLE students 
ADD CONSTRAINT fk_students_curriculum_id 
FOREIGN KEY (curriculum_id) REFERENCES curriculum(id) ON DELETE SET NULL;

-- Add unique constraint for user_id
ALTER TABLE students 
ADD UNIQUE KEY uq_students_user_id (user_id);

-- Step 3: Update Curriculum table to match new structure
-- Remove subject_id and semester from curriculum table
ALTER TABLE curriculum 
DROP COLUMN IF EXISTS subject_id,
DROP COLUMN IF EXISTS semester,
DROP COLUMN IF EXISTS is_active;

-- Add missing columns if they don't exist
ALTER TABLE curriculum 
ADD COLUMN IF NOT EXISTS year_level INT NOT NULL DEFAULT 1 AFTER course_id,
ADD COLUMN IF NOT EXISTS section VARCHAR(20) NOT NULL DEFAULT '' AFTER year_level,
ADD COLUMN IF NOT EXISTS created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER section;

-- Step 4: Update Signatory table to match new structure
-- Remove organization_id, position, sort_order, is_active from signatories table
ALTER TABLE signatories 
DROP COLUMN IF EXISTS organization_id,
DROP COLUMN IF EXISTS position,
DROP COLUMN IF EXISTS sort_order,
DROP COLUMN IF EXISTS is_active;

-- Add missing columns if they don't exist
ALTER TABLE signatories 
ADD COLUMN IF NOT EXISTS employee_id VARCHAR(50) NOT NULL DEFAULT '' AFTER user_id,
ADD COLUMN IF NOT EXISTS signature_data MEDIUMTEXT DEFAULT NULL AFTER employee_id,
ADD COLUMN IF NOT EXISTS created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP AFTER signature_data;

-- Step 5: Create StudentSignatory table if it doesn't exist
CREATE TABLE IF NOT EXISTS student_signatories (
    id             INT          AUTO_INCREMENT PRIMARY KEY,
    user_id        INT          NOT NULL,
    position       VARCHAR(100)           DEFAULT '',
    signature_data MEDIUMTEXT             DEFAULT NULL,
    created_at     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- Step 6: Update Users table to match new structure
-- Add missing columns if they don't exist
ALTER TABLE users 
ADD COLUMN IF NOT EXISTS suffix_name VARCHAR(20) DEFAULT '' AFTER last_name,
ADD COLUMN IF NOT EXISTS id_number VARCHAR(50) DEFAULT NULL AFTER password;

-- Step 7: Data Migration (if needed)
-- This section would need to be customized based on your existing data
-- Example: Migrate student data from old structure to new structure
-- UPDATE students s JOIN users u ON s.student_id = u.id_number 
-- SET s.user_id = u.id, s.student_number = s.student_id;

-- ============================================================
-- Verification Queries
-- ============================================================

-- Check table structures
DESCRIBE users;
DESCRIBE students;
DESCRIBE curriculum;
DESCRIBE signatories;
DESCRIBE student_signatories;

-- Check foreign key constraints
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CONSTRAINT_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE 
WHERE TABLE_SCHEMA = DATABASE() 
AND REFERENCED_TABLE_NAME IS NOT NULL;
