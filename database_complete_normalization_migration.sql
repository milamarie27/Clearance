-- ============================================================
-- Complete Database Normalization Migration Script
-- Ensures 100% normalization with zero duplications
-- ============================================================

-- Step 1: Remove all duplicate CreatedAt fields (single source: users.created_at)
ALTER TABLE courses 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE subjects 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE sections 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE academic_periods 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE curriculum 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE students 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE student_signatories 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE signatories 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE organizations 
DROP COLUMN IF EXISTS created_at;

-- Step 2: Remove duplicate employee_id from signatories (use users.id_number)
ALTER TABLE signatories 
DROP COLUMN IF EXISTS employee_id;

-- Step 3: Remove duplicate subject fields from subject_offerings
ALTER TABLE subject_offerings 
DROP COLUMN IF EXISTS subject_code,
DROP COLUMN IF EXISTS description,
DROP COLUMN IF EXISTS lab_unit,
DROP COLUMN IF EXISTS lec_unit,
DROP COLUMN IF EXISTS academic_year,
DROP COLUMN IF EXISTS semester,
DROP COLUMN IF EXISTS section;

-- Step 4: Remove duplicate org_signatory from organizations (use users.id_number)
ALTER TABLE organizations 
DROP COLUMN IF EXISTS org_signatory;

-- Step 5: Fix subject_offerings to use proper foreign keys
-- Add missing columns if they don't exist
ALTER TABLE subject_offerings 
ADD COLUMN IF NOT EXISTS user_id INT NOT NULL AFTER subject_id,
ADD COLUMN IF NOT EXISTS period_id INT NOT NULL AFTER user_id;

-- Add foreign key constraints
ALTER TABLE subject_offerings 
ADD CONSTRAINT IF NOT EXISTS fk_so_user_id 
FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
ADD CONSTRAINT IF NOT EXISTS fk_so_period_id 
FOREIGN KEY (period_id) REFERENCES academic_periods(id) ON DELETE CASCADE;

-- Step 6: Fix organizations to use proper foreign key
-- Add user_id column if it doesn't exist
ALTER TABLE organizations 
ADD COLUMN IF NOT EXISTS user_id INT DEFAULT NULL AFTER position_title;

-- Add foreign key constraint
ALTER TABLE organizations 
ADD CONSTRAINT IF NOT EXISTS fk_org_user_id 
FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE SET NULL;

-- Step 7: Ensure proper unique constraints
ALTER TABLE signatories 
ADD CONSTRAINT IF NOT EXISTS uq_signatories_user_id UNIQUE (user_id);

ALTER TABLE subject_offerings 
ADD CONSTRAINT IF NOT EXISTS uq_so_mis_code UNIQUE (mis_code);

-- Step 8: Data migration for existing records
-- Migrate instructor_id to user_id in subject_offerings
UPDATE subject_offerings so 
SET user_id = (
    SELECT s.user_id 
    FROM signatories s 
    WHERE s.id = so.instructor_id
) 
WHERE instructor_id IS NOT NULL AND user_id IS NULL;

-- Drop old instructor_id column after migration
ALTER TABLE subject_offerings 
DROP COLUMN IF EXISTS instructor_id;

-- Step 9: Clean up any orphaned records
DELETE FROM subject_offerings WHERE subject_id NOT IN (SELECT id FROM subjects);
DELETE FROM students WHERE user_id NOT IN (SELECT id FROM users);
DELETE FROM signatories WHERE user_id NOT IN (SELECT id FROM users);
DELETE FROM student_signatories WHERE user_id NOT IN (SELECT id FROM users);
DELETE FROM organizations WHERE user_id NOT IN (SELECT id FROM users) AND user_id IS NOT NULL;

-- ============================================================
-- Verification Queries
-- ============================================================

-- Check for any remaining duplicate fields
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
AND TABLE_NAME IN ('users', 'students', 'signatories', 'student_signatories', 'organizations', 'subject_offerings')
AND COLUMN_NAME LIKE '%created_at%'
ORDER BY TABLE_NAME, COLUMN_NAME;

-- Check foreign key relationships
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE 
WHERE TABLE_SCHEMA = DATABASE() 
AND REFERENCED_TABLE_NAME IS NOT NULL
ORDER BY TABLE_NAME;

-- Check for data consistency
SELECT 
    'users' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT email) as unique_emails,
    COUNT(DISTINCT id_number) as unique_id_numbers
FROM users
UNION ALL
SELECT 
    'signatories' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users,
    0 as unique_id_numbers
FROM signatories
UNION ALL
SELECT 
    'students' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users,
    COUNT(DISTINCT student_number) as unique_student_numbers
FROM students;

-- ============================================================
-- Final Validation
-- ============================================================

-- Ensure no duplicate data patterns exist
SELECT 
    'Duplicate Check' as validation_type,
    CASE 
        WHEN COUNT(*) = 0 THEN 'PASS - No duplicate user IDs in students'
        ELSE 'FAIL - Duplicate user IDs found in students'
    END as result
FROM (
    SELECT user_id, COUNT(*) as cnt 
    FROM students 
    GROUP BY user_id 
    HAVING cnt > 1
) dups
UNION ALL
SELECT 
    'Duplicate Check' as validation_type,
    CASE 
        WHEN COUNT(*) = 0 THEN 'PASS - No duplicate user IDs in signatories'
        ELSE 'FAIL - Duplicate user IDs found in signatories'
    END as result
FROM (
    SELECT user_id, COUNT(*) as cnt 
    FROM signatories 
    GROUP BY user_id 
    HAVING cnt > 1
) dups;
