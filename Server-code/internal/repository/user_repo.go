package repository

import (
	"errors"

	"labelpro-server/internal/models"

	"github.com/google/uuid"
	"gorm.io/gorm"
)

type UserRepository struct {
	db *gorm.DB
}

func NewUserRepository(db *gorm.DB) *UserRepository {
	return &UserRepository{db: db}
}

func (r *UserRepository) Create(user *models.User) error {
	return r.db.Create(user).Error
}

func (r *UserRepository) FindByID(id string) (*models.User, error) {
	var user models.User
	err := r.db.Preload("Department").First(&user, "id = ?", id).Error
	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, err
	}
	return &user, nil
}

func (r *UserRepository) FindByUsername(username string) (*models.User, error) {
	var user models.User
	err := r.db.Preload("Department").First(&user, "username = ?", username).Error
	if err != nil {
		if errors.Is(err, gorm.ErrRecordNotFound) {
			return nil, nil
		}
		return nil, err
	}
	return &user, nil
}

type UserFilter struct {
	DeptID   string
	Role     string
	Keyword  string
	Page     int
	PageSize int
}

func (r *UserRepository) List(filter UserFilter) ([]models.User, int64, error) {
	var users []models.User
	var total int64

	query := r.db.Model(&models.User{}).Preload("Department")

	if filter.DeptID != "" {
		query = query.Where("department_id = ?", filter.DeptID)
	}
	if filter.Role != "" {
		query = query.Where("role = ?", filter.Role)
	}
	if filter.Keyword != "" {
		keyword := "%" + filter.Keyword + "%"
		query = query.Where("name LIKE ? OR username LIKE ?", keyword, keyword)
	}

	if err := query.Count(&total).Error; err != nil {
		return nil, 0, err
	}

	offset := (filter.Page - 1) * filter.PageSize
	if err := query.Offset(offset).Limit(filter.PageSize).Order("created_at DESC").Find(&users).Error; err != nil {
		return nil, 0, err
	}

	return users, total, nil
}

func (r *UserRepository) FindVisibleUsers(userID string, deptID string) ([]models.User, error) {
	var users []models.User
	err := r.db.Where("id != ?", userID).
		Where("is_active = ?", true).
		Order("name ASC").
		Find(&users).Error
	return users, err
}

func (r *UserRepository) FindAllActiveUsers(userID string) ([]models.User, error) {
	var users []models.User
	err := r.db.Where("id != ?", userID).
		Where("is_active = ?", true).
		Preload("Department").
		Order("name ASC").
		Find(&users).Error
	return users, err
}

func (r *UserRepository) Update(user *models.User) error {
	return r.db.Omit("Department").Save(user).Error
}

func (r *UserRepository) Delete(id string) error {
	uid, err := uuid.Parse(id)
	if err != nil {
		return err
	}
	return r.db.Delete(&models.User{}, "id = ?", uid).Error
}

func (r *UserRepository) UpdateLastLogin(id string) error {
	return r.db.Model(&models.User{}).Where("id = ?", id).
		Update("last_login_at", gorm.Expr("NOW()")).Error
}
