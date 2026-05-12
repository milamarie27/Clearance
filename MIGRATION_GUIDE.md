# Database Migration Guide
## Complete Normalization Process

### 🚨 IMPORTANT: Backup First
```sql
-- Create full database backup before starting
mysqldump -u username -p database_name > backup_before_migration.sql
```

---

## 📋 Migration Steps

### Step 1: Run Initial Cleanup Migration
```bash
# Execute the first migration script
mysql -u username -p database_name < database_cleanup_migration.sql
```

### Step 2: Run Simplification Migration
```bash
# Execute the simplification script
mysql -u username -p database_name < database_simplification_migration.sql
```

### Step 3: Run Complete Normalization Migration
```bash
# Execute the final normalization script
mysql -u username -p database_name < database_complete_normalization_migration.sql
```

### Step 4: Apply Final Schema (Optional - for new databases)
```bash
# If starting fresh, use this instead
mysql -u username -p database_name < database_final_normalized_schema.sql
```

---

## 🔍 Verification Steps

### After Each Migration, Run These Checks:

```sql
-- 1. Check table structures
DESCRIBE users;
DESCRIBE students;
DESCRIBE signatories;
DESCRIBE organizations;
DESCRIBE subject_offerings;

-- 2. Verify foreign keys
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    REFERENCED_TABLE_NAME,
    REFERENCED_COLUMN_NAME
FROM information_schema.KEY_COLUMN_USAGE 
WHERE TABLE_SCHEMA = DATABASE() 
AND REFERENCED_TABLE_NAME IS NOT NULL;

-- 3. Check for duplicate data
SELECT 
    'signatories' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users
FROM signatories
UNION ALL
SELECT 
    'students' as table_name,
    COUNT(*) as total_records,
    COUNT(DISTINCT user_id) as unique_users
FROM students;
```

---

## ⚠️ Common Issues & Solutions

### Issue 1: Foreign Key Constraints Fail
**Solution**: Check for orphaned records first
```sql
-- Find orphaned records
SELECT * FROM students WHERE user_id NOT IN (SELECT id FROM users);
SELECT * FROM signatories WHERE user_id NOT IN (SELECT id FROM users);

-- Clean them up
DELETE FROM students WHERE user_id NOT IN (SELECT id FROM users);
DELETE FROM signatories WHERE user_id NOT IN (SELECT id FROM users);
```

### Issue 2: Duplicate User IDs
**Solution**: Identify and resolve duplicates
```sql
-- Find duplicate user_ids in signatories
SELECT user_id, COUNT(*) as count 
FROM signatories 
GROUP BY user_id 
HAVING count > 1;

-- Find duplicate user_ids in students
SELECT user_id, COUNT(*) as count 
FROM students 
GROUP BY user_id 
HAVING count > 1;
```

### Issue 3: Missing ID Numbers
**Solution**: Update users with missing ID numbers
```sql
-- Find users without ID numbers who are signatories
SELECT u.id, u.first_name, u.last_name, u.email
FROM users u
JOIN signatories s ON u.id = s.user_id
WHERE u.id_number IS NULL OR u.id_number = '';
```

---

## 🔄 Rollback Procedure

### If Migration Fails:
```sql
-- Restore from backup
mysql -u username -p database_name < backup_before_migration.sql
```

### Partial Rollback:
```sql
-- If you need to undo specific changes
-- (This is complex, full backup restore is recommended)
```

---

## 📊 Post-Migration Validation

### Final Validation Queries:
```sql
-- 1. Ensure no duplicate created_at fields
SELECT TABLE_NAME, COLUMN_NAME 
FROM information_schema.COLUMNS 
WHERE TABLE_SCHEMA = DATABASE() 
AND COLUMN_NAME = 'created_at'
AND TABLE_NAME != 'users'
AND TABLE_NAME != 'announcements';

-- 2. Verify all relationships are intact
SELECT 
    'users' as table_name,
    COUNT(*) as total_users,
    COUNT(DISTINCT email) as unique_emails
FROM users
UNION ALL
SELECT 
    'students' as table_name,
    COUNT(*) as total_students,
    COUNT(DISTINCT user_id) as unique_user_ids
FROM students
UNION ALL
SELECT 
    'signatories' as table_name,
    COUNT(*) as total_signatories,
    COUNT(DISTINCT user_id) as unique_user_ids
FROM signatories;

-- 3. Check data integrity
SELECT 
    CASE 
        WHEN COUNT(*) = 0 THEN 'PASS - No orphaned students'
        ELSE 'FAIL - Orphaned students found'
    END as validation_result
FROM students s
LEFT JOIN users u ON s.user_id = u.id
WHERE u.id IS NULL;
```

---

## 🚀 Application Updates Required

### After Database Migration:

1. **Update Entity Framework Models**:
   - All models are already updated in this project
   - Ensure your DbContext references the correct models

2. **Update Application Code**:
   - Replace `signatory.EmployeeId` with `signatory.User.IdNumber`
   - Remove references to deleted `CreatedAt` fields (except `users.created_at`)
   - Update subject offering queries to use navigation properties

3. **Test Application**:
   - Test user registration/login
   - Test student clearance processes
   - Test signatory functionality
   - Test organization management

---

## 📞 Support

### If You Encounter Issues:
1. Check the MySQL error logs
2. Run the verification queries above
3. Restore from backup if necessary
4. Contact your database administrator

### Migration Success Indicators:
✅ All foreign key constraints are satisfied  
✅ No duplicate data exists  
✅ All application features work correctly  
✅ No orphaned records in any table  
✅ All validation queries return "PASS"
