# Database Design

## Overview

Database được sử dụng cho hệ thống Vision AI đếm số lượng nguyên vật liệu sử dụng:

* WPF (.NET 8)
* Basler Camera
* YOLO ONNX
* SQL Server

Hệ thống không lưu dữ liệu phục vụ training AI mà tập trung vào:

* Quản lý model ONNX
* Quản lý class trong model
* Quản lý vật tư (Item)
* Lưu lịch sử detect
* Lưu số lượng vật tư đếm được
* Lưu hình ảnh kết quả
* Audit log và system log
* Truy xuất lịch sử sản xuất

---

# Database Architecture

```text
Classes
   │
   └── Items
        
Machines
    │
    └── Detections
                │
                └── DetectionDetails

Machines
    │
    └── Logs
```

---

# 1. Models

Lưu thông tin các model ONNX được sử dụng trong hệ thống.

## Table

```sql
models
```

## Columns

| Column        | Type     | Description             |
| ------------- | -------- | ----------------------- |
| id            | int      | Primary Key             |
| model_code    | varchar  | Mã model duy nhất       |
| model_name    | varchar  | Tên model               |
| model_path    | varchar  | Đường dẫn file ONNX     |
| is_active     | boolean  | Model đang được sử dụng |
| description   | text     | Ghi chú                 |
| created_at    | datetime | Thời gian tạo           |

## Example

```text
model_code     : YOLO_MATERIAL_V1
model_name     : Material Counter  
model_path     : D:\Vision\Models\v1\best.onnx
```

---

# 2. Classes

Lưu thông tin class của từng model.

Class ID tương ứng với output của YOLO.

## Table

```sql
classes
```

## Columns

| Column      | Type     | Description          |
| ----------- | -------- | -------------------- |
| id          | int      | Primary Key          |
| class_code  | int      | Class ID trong model |
| class_name  | varchar  | Tên class            |
| description | text     | Mô tả                |
| created_at  | datetime | Thời gian tạo        |

## Example

```text
class_code : 0
class_name : Screw

class_code : 1
class_name : Nut

class_code : 2
class_name : Washer
```

---

# 3. Items

Lưu thông tin vật tư thực tế trong hệ thống.

Một Item thuộc một Class.

## Table

```sql
items
```

## Columns

| Column     | Type     | Description        |
| ---------- | -------- | ------------------ |
| id         | int      | Primary Key        |
| class_id   | int      | FK tới classes     |
| item_code  | varchar  | Mã vật tư          |
| item_name  | varchar  | Tên vật tư         |
| item_type  | enum     | Loại vật tư        |
| is_active  | boolean  | Trạng thái sử dụng |
| created_at | datetime | Thời gian tạo      |

## Example

```text
item_code : SCR-M4-10
item_name : Screw M4 x 10

item_type : screw
```

---

# 4. Machines

Lưu thông tin các máy Vision trong hệ thống.

Mỗi máy tương ứng một WPF Client.

## Table

```sql
machines
```

## Columns

| Column       | Type     | Description                  |
| ------------ | -------- | ---------------------------- |
| id           | int      | Primary Key                  |
| hostname     | varchar  | Tên host                     |
| machine_name | varchar  | Tên máy                      |
| ip           | varchar  | Địa chỉ IP                   |
| status       | varchar  | Online / Offline             |
| last_seen    | datetime | Thời gian heartbeat gần nhất |
| created_at   | datetime | Thời gian tạo                |

## Example

```text
hostname     : VISION-PC-01
machine_name : Line-A
ip           : 192.168.1.10
status       : Online
```

---

# 5. Detections

Lưu lịch sử mỗi lần hệ thống thực hiện detect.

Đây là bảng trung tâm của hệ thống.

## Table

```sql
detections
```

## Columns

| Column             | Type     | Description                |
| ------------------ | -------- | -------------------------- |
| id                 | bigint   | Primary Key                |
| machine_id         | int      | Máy thực hiện detect       |
| item_id            | int      | Item chính được detect     |
| model_id           | int      | Model đang sử dụng         |
| processing_time_ms | float    | Thời gian xử lý            |
| image_path         | varchar  | Ảnh gốc                    |
| result_image_path  | varchar  | Ảnh kết quả                |
| total_objects      | int      | Tổng số object detect được |
| created_at         | datetime | Thời gian detect           |

## Example

```text
machine_id         : 1
item_id            : 5
model_id           : 2

processing_time_ms : 28.5

total_objects      : 156

image_path         :
D:\Images\Raw\20260617_090101.jpg

result_image_path  :
D:\Images\Result\20260617_090101.jpg
```

---

# 6. Detection Details

Lưu kết quả chi tiết của từng class trong một lần detect.

Một Detection có thể có nhiều Detection Detail.

## Table

```sql
detection_details
```

## Columns

| Column         | Type     | Description                |
| -------------- | -------- | -------------------------- |
| id             | bigint   | Primary Key                |
| detection_id   | bigint   | FK tới detections          |
| item_id        | int      | Item được detect           |
| quantity       | int      | Số lượng detect được       |
| avg_confidence | float    | Confidence trung bình      |
| result_json    | text     | Dữ liệu chi tiết dạng JSON |
| created_at     | datetime | Thời gian tạo              |

## Example

```text
item_id        : 10

quantity       : 125

avg_confidence : 0.94
```

### Sample JSON

```json
{
  "class_id": 0,
  "count": 125,
  "avg_confidence": 0.94
}
```

### Purpose

Bảng này được sử dụng cho:

* Thống kê số lượng
* Dashboard
* Báo cáo sản lượng
* Truy xuất lịch sử detect

---

# 7. Logs

Lưu audit log và system log.

## Table

```sql
logs
```

## Columns

| Column       | Type     | Description                   |
| ------------ | -------- | ----------------------------- |
| id           | bigint   | Primary Key                   |
| machine_id   | int      | Máy thực hiện hành động       |
| detection_id | bigint   | Liên kết detect (nếu có)      |
| action       | enum     | Loại hành động                |
| actor        | varchar  | Người thực hiện hoặc hệ thống |
| description  | text     | Nội dung chi tiết             |
| created_at   | datetime | Thời gian ghi log             |

---

# Log Actions

## Detection

```text
DETECT
```

Ghi nhận một lần detect hoàn thành.

---

## Item Management

```text
ITEM_CREATE
ITEM_UPDATE
ITEM_DELETE
```

Ghi nhận thay đổi vật tư.

---

## Model Management

```text
MODEL_CREATE
MODEL_UPDATE
MODEL_SWITCH
```

Ghi nhận thay đổi model.

Ví dụ:

```text
Switch model

v1.0
→
v1.1
```

---

## System Events

```text
SYSTEM_START
SYSTEM_STOP
```

Ghi nhận trạng thái hệ thống.

---

## Errors

```text
ERROR
```

Ghi nhận lỗi hệ thống.

Ví dụ:

```text
Camera disconnected

Model load failed

SQL connection timeout
```

---

# WPF Data Flow

## Startup

```text
WPF Application

    ↓

Load Active Model

    ↓

Load Classes

    ↓

Load Items

    ↓

Connect Camera
```

---

## Detection Flow

```text
Camera Grab

    ↓

YOLO Predict

    ↓

Create Detection

    ↓

Create Detection Details

    ↓

Save Images

    ↓

Write Logs
```

---

# Data Retention

## Images

Khuyến nghị:

```text
Raw Images
    30 ngày

Result Images
    90 ngày
```

## Logs

Khuyến nghị:

```text
1 năm
```

## Detection History

Khuyến nghị:

```text
Không xóa
```

Sử dụng để:

* Truy xuất sản lượng
* Báo cáo
* Audit
* Thống kê hiệu suất hệ thống

---

# Summary

Database được thiết kế theo hướng:

* Hỗ trợ nhiều model ONNX
* Hỗ trợ nhiều class trên từng model
* Hỗ trợ nhiều máy Vision
* Lưu lịch sử detect
* Lưu số lượng vật tư đếm được
* Lưu ảnh kết quả
* Audit toàn bộ thay đổi hệ thống
* Phù hợp triển khai trực tiếp từ WPF tới SQL Server không cần Backend API
