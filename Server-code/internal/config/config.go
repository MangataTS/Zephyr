package config

import (
	"encoding/json"
	"fmt"
	"os"
	"strings"
	"sync"
)

var (
	activeConfig *Config
	configMu     sync.RWMutex
	configPath   string
)

type Config struct {
	Server    ServerConfig    `json:"server"`
	Database  DatabaseConfig  `json:"database"`
	Redis     RedisConfig     `json:"redis"`
	JWT       JWTConfig       `json:"jwt"`
	Log       LogConfig       `json:"log"`
	Storage   StorageConfig   `json:"storage"`
	WebSocket WebSocketConfig `json:"websocket"`
	RateLimit RateLimitConfig `json:"rate_limit"`
	Security  SecurityConfig  `json:"security"`
	Scheduler SchedulerConfig `json:"scheduler"`
	Features  FeaturesConfig  `json:"features"`
}

type ServerConfig struct {
	Port                int    `json:"port"`
	Host                string `json:"host"`
	Mode                string `json:"mode"`
	ReadTimeoutSeconds  int    `json:"read_timeout_seconds"`
	WriteTimeoutSeconds int    `json:"write_timeout_seconds"`
	MaxRequestBodyMB    int    `json:"max_request_body_mb"`
}

type DatabaseConfig struct {
	Host                   string `json:"host"`
	Port                   int    `json:"port"`
	User                   string `json:"user"`
	Password               string `json:"password"`
	DBName                 string `json:"dbname"`
	SSLMode                string `json:"sslmode"`
	MaxOpenConns           int    `json:"max_open_conns"`
	MaxIdleConns           int    `json:"max_idle_conns"`
	ConnMaxLifetimeSeconds int    `json:"conn_max_lifetime_seconds"`
	LogLevel               string `json:"log_level"`
}

type RedisConfig struct {
	Host                string `json:"host"`
	Port                int    `json:"port"`
	Password            string `json:"password"`
	DB                  int    `json:"db"`
	PoolSize            int    `json:"pool_size"`
	MinIdleConns        int    `json:"min_idle_conns"`
	DialTimeoutSeconds  int    `json:"dial_timeout_seconds"`
	ReadTimeoutSeconds  int    `json:"read_timeout_seconds"`
	WriteTimeoutSeconds int    `json:"write_timeout_seconds"`
}

type JWTConfig struct {
	PrivateKeyPath            string `json:"private_key_path"`
	PublicKeyPath             string `json:"public_key_path"`
	AccessTokenExpireSeconds  int    `json:"access_token_expire_seconds"`
	RefreshTokenExpireSeconds int    `json:"refresh_token_expire_seconds"`
	Issuer                    string `json:"issuer"`
}

type LogConfig struct {
	Level         string `json:"level"`
	Format        string `json:"format"`
	OutputDir     string `json:"output_dir"`
	MaxSizeMB     int    `json:"max_size_mb"`
	MaxBackups    int    `json:"max_backups"`
	MaxAgeDays    int    `json:"max_age_days"`
	Compress      bool   `json:"compress"`
	EnableConsole bool   `json:"enable_console"`
}

type StorageConfig struct {
	Type              string      `json:"type"`
	LocalPath         string      `json:"local_path"`
	MaxFileSizeMB     int         `json:"max_file_size_mb"`
	AllowedExtensions []string    `json:"allowed_extensions"`
	AllowedMimeTypes  []string    `json:"allowed_mime_types"`
	Minio             MinioConfig `json:"minio"`
}

type MinioConfig struct {
	Endpoint  string `json:"endpoint"`
	AccessKey string `json:"access_key"`
	SecretKey string `json:"secret_key"`
	Bucket    string `json:"bucket"`
	UseSSL    bool   `json:"use_ssl"`
}

type WebSocketConfig struct {
	Enabled                  bool `json:"enabled"`
	HeartbeatIntervalSeconds int  `json:"heartbeat_interval_seconds"`
	MaxConnectionsPerUser    int  `json:"max_connections_per_user"`
	CanvasSyncThrottleMs     int  `json:"canvas_sync_throttle_ms"`
}

type RateLimitConfig struct {
	Enabled            bool `json:"enabled"`
	LoginPerMinute     int  `json:"login_per_minute"`
	APIPerMinute       int  `json:"api_per_minute"`
	BanDurationSeconds int  `json:"ban_duration_seconds"`
}

type SecurityConfig struct {
	BcryptCost             int      `json:"bcrypt_cost"`
	PasswordMinLength      int      `json:"password_min_length"`
	PasswordRequireUpper   bool     `json:"password_require_upper"`
	PasswordRequireLower   bool     `json:"password_require_lower"`
	PasswordRequireDigit   bool     `json:"password_require_digit"`
	PasswordRequireSpecial bool     `json:"password_require_special"`
	MaxLoginAttempts       int      `json:"max_login_attempts"`
	LoginLockoutMinutes    int      `json:"login_lockout_minutes"`
	CORSAllowedOrigins     []string `json:"cors_allowed_origins"`
	EnableCSRF             bool     `json:"enable_csrf"`
}

type SchedulerConfig struct {
	AutoRemindEnabled         bool `json:"auto_remind_enabled"`
	AutoRemindIntervalMinutes int  `json:"auto_remind_interval_minutes"`
	AutoArchiveEnabled        bool `json:"auto_archive_enabled"`
	ExportTaskTimeoutSeconds  int  `json:"export_task_timeout_seconds"`
}

type FeaturesConfig struct {
	SMSNotification        bool `json:"sms_notification"`
	DingTalkNotification   bool `json:"dingtalk_notification"`
	WeChatWorkNotification bool `json:"wechat_work_notification"`
	DemoMode               bool `json:"demo_mode"`
}

func Load(path string) (*Config, error) {
	data, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("failed to read config file: %w", err)
	}

	var cfg Config
	if err := json.Unmarshal(data, &cfg); err != nil {
		return nil, fmt.Errorf("failed to parse config file: %w", err)
	}

	cfg.applyEnvOverrides()
	cfg.setDefaults()

	return &cfg, nil
}

func (c *Config) applyEnvOverrides() {
	if v := os.Getenv("LABELPRO_SERVER_PORT"); v != "" {
		fmt.Sscanf(v, "%d", &c.Server.Port)
	}
	if v := os.Getenv("LABELPRO_SERVER_MODE"); v != "" {
		c.Server.Mode = v
	}
	if v := os.Getenv("LABELPRO_DATABASE_HOST"); v != "" {
		c.Database.Host = v
	}
	if v := os.Getenv("LABELPRO_DATABASE_PORT"); v != "" {
		fmt.Sscanf(v, "%d", &c.Database.Port)
	}
	if v := os.Getenv("LABELPRO_DATABASE_USER"); v != "" {
		c.Database.User = v
	}
	if v := os.Getenv("LABELPRO_DATABASE_PASSWORD"); v != "" {
		c.Database.Password = v
	}
	if v := os.Getenv("LABELPRO_DATABASE_DBNAME"); v != "" {
		c.Database.DBName = v
	}
	if v := os.Getenv("LABELPRO_REDIS_HOST"); v != "" {
		c.Redis.Host = v
	}
	if v := os.Getenv("LABELPRO_REDIS_PASSWORD"); v != "" {
		c.Redis.Password = v
	}
}

func (c *Config) setDefaults() {
	if c.Server.Port == 0 {
		c.Server.Port = 8080
	}
	if c.Server.Host == "" {
		c.Server.Host = "0.0.0.0"
	}
	if c.Server.Mode == "" {
		c.Server.Mode = "debug"
	}
	if c.Server.ReadTimeoutSeconds == 0 {
		c.Server.ReadTimeoutSeconds = 30
	}
	if c.Server.WriteTimeoutSeconds == 0 {
		c.Server.WriteTimeoutSeconds = 200
	}
	if c.Database.Port == 0 {
		c.Database.Port = 5432
	}
	if c.Database.SSLMode == "" {
		c.Database.SSLMode = "disable"
	}
	if c.Database.MaxOpenConns == 0 {
		c.Database.MaxOpenConns = 50
	}
	if c.Database.MaxIdleConns == 0 {
		c.Database.MaxIdleConns = 10
	}
	if c.Redis.Port == 0 {
		c.Redis.Port = 6379
	}
	if c.Redis.PoolSize == 0 {
		c.Redis.PoolSize = 20
	}
	if c.Log.Level == "" {
		c.Log.Level = "info"
	}
	if c.Log.OutputDir == "" {
		c.Log.OutputDir = "./logs"
	}
	if c.Security.BcryptCost == 0 {
		c.Security.BcryptCost = 12
	}
	if c.Security.PasswordMinLength == 0 {
		c.Security.PasswordMinLength = 8
	}
	if c.JWT.AccessTokenExpireSeconds == 0 {
		c.JWT.AccessTokenExpireSeconds = 7200
	}
	if c.JWT.RefreshTokenExpireSeconds == 0 {
		c.JWT.RefreshTokenExpireSeconds = 604800
	}
}

func (c *Config) DSN() string {
	return fmt.Sprintf(
		"host=%s port=%d user=%s password=%s dbname=%s sslmode=%s",
		c.Database.Host, c.Database.Port, c.Database.User, c.Database.Password, c.Database.DBName, c.Database.SSLMode,
	)
}

func (c *Config) RedisAddr() string {
	return fmt.Sprintf("%s:%d", c.Redis.Host, c.Redis.Port)
}

func (c *Config) ServerAddr() string {
	return fmt.Sprintf("%s:%d", c.Server.Host, c.Server.Port)
}

func validateNotEmpty(s string, field string) error {
	if strings.TrimSpace(s) == "" {
		return fmt.Errorf("config field '%s' is required but empty", field)
	}
	return nil
}

func SetActive(cfg *Config, path string) {
	configMu.Lock()
	defer configMu.Unlock()
	activeConfig = cfg
	configPath = path
}

func GetActive() *Config {
	configMu.RLock()
	defer configMu.RUnlock()
	return activeConfig
}

func GetConfigPath() string {
	configMu.RLock()
	defer configMu.RUnlock()
	return configPath
}

func ReloadConfig() (*Config, error) {
	configMu.RLock()
	path := configPath
	configMu.RUnlock()

	cfg, err := Load(path)
	if err != nil {
		return nil, err
	}

	configMu.Lock()
	activeConfig = cfg
	configMu.Unlock()

	return cfg, nil
}
