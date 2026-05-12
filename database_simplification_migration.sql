-- ============================================================
-- Database Simplification Migration Script
-- Further simplification to eliminate duplicate ID fields and redundant columns
-- ============================================================

-- Step 1: Remove duplicate EmployeeId from signatories table
-- The employee_id is redundant since we can use users.id_number
ALTER TABLE signatories 
DROP COLUMN IF EXISTS employee_id;

-- Step 2: Remove redundant created_at fields from tables
-- These fields are redundant since users.created_at can be used
ALTER TABLE students 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE curriculum 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE student_signatories 
DROP COLUMN IF EXISTS created_at;

ALTER TABLE signatories 
DROP COLUMN IF EXISTS created_at;

-- Step 3: Ensure proper unique constraints
-- Make sure signatories has unique constraint on user_id
ALTER TABLE signatories 
ADD CONSTRAINT IF NOT EXISTS uq_signatories_user_id UNIQUE (user_id);

-- Step 4: Data consistency check
-- Verify that all signatory users have id_number values
SELECT 
    s.id as signatory_id,
    s.user_id,
    u.id_number,
    u.first_name,
    u.last_name
FROM signatories s
JOIN users u ON s.user_id = u.id
WHERE u.id_number IS NULL OR u.id_number = '';

-- Step 5: Update application code references
-- The following code changes need to be made in the application:
-- - Replace signatory.EmployeeId with signatory.User.IdNumber
-- - Remove references to created_at fields in Student, Curriculum, StudentSignatory, Signatory models
-- - Update any SQL queries that reference the removed columns

-- ============================================================
-- Verification Queries
-- ============================================================

-- Check final table structures
DESCRIBE users;
DESCRIBE students;
DESCRIBE curriculum;
DESCRIBE signatories;
DESCRIBE student_signatories;

-- Verify foreign key relationships
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    CONSTRAINT_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE 
WHERE TABLE_SCHEMA = DATABASE() 
AND REFERENCED_TABLE_NAME IS NOT NULL
AND TABLE_NAME IN ('students', 'curriculum', 'signatories', 'student_signatories');

-- Check for any remaining duplicate ID patterns
SELECT 
    'signatories' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users
FROM signatories
UNION ALL
SELECT 
    'student_signatories' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users
FROM student_signatories;
