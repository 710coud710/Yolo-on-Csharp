Viewed HistoryViewModel.cs:1-41

Dưới đây là mô tả chi tiết về chức năng của các thư mục, luồng hoạt động chính của ứng dụng và hướng dẫn vị trí đặt file mô hình `best.onnx`.

---

# 1. Chức năng của các Thư mục (Folder Roles)

*   **`Commands/`** (Lệnh điều hướng):
    *   Chứa các class triển khai interface `ICommand` của WPF (ví dụ: [RelayCommand.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Commands/RelayCommand.cs)).
    *   **Mục đích:** Thay vì viết mã xử lý sự kiện click trực tiếp trong file giao diện code-behind (`MainWindow.xaml.cs`), WPF MVVM liên kết (`Bind`) thuộc tính `Command` của Button trực tiếp tới thuộc tính `ICommand` trong ViewModel. `RelayCommand` đóng vai trò chuyển tiếp hành động này tới một phương thức cụ thể trong ViewModel.
*   **`Converters/`** (Bộ chuyển đổi dữ liệu):
    *   Chứa các bộ chuyển đổi triển khai `IValueConverter` (ví dụ: `BoolToVisibilityConverter.cs`).
    *   **Mục đích:** Chuyển đổi định dạng dữ liệu tự động phục vụ hiển thị UI. Ví dụ: Nhận vào giá trị kiểu `bool` (`true/false`) và chuyển đổi sang thuộc tính `Visibility` (`Visible/Collapsed`) để ẩn/hiện một Control trên giao diện WPF.
*   **`Constants/`** (Hằng số hệ thống):
    *   Lưu trữ cấu hình tĩnh mặc định của hệ thống (ví dụ: [DefaultSettings.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Constants/DefaultSettings.cs)).
    *   **Mục đích:** Tránh việc hardcode các giá trị chuỗi hoặc số (như đường dẫn mặc định, ngưỡng tin cậy mặc định `0.25`) rải rác trong code, giúp dễ cấu hình tập trung.
*   **`ViewModels/`** (Lớp xử lý logic giao diện):
    *   Thành phần cốt lõi quản lý dữ liệu và nghiệp vụ cho từng màn hình tương ứng (ví dụ: [DashboardViewModel.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/ViewModels/DashboardViewModel.cs) quản lý dữ liệu cho màn hình Dashboard).
    *   **Mục đích:** ViewModel kế thừa từ `ViewModelBase` (phát sự kiện thông báo thay đổi giao diện qua `INotifyPropertyChanged`). Nó tiếp nhận tương tác từ UI thông qua Commands, xử lý, cập nhật thuộc tính và tự động thay đổi những gì hiển thị trên màn hình View mà không cần can thiệp trực tiếp vào UI Controls.
*   **`Services/`** (Tầng dịch vụ lõi nghiệp vụ):
    *   Chứa các dịch vụ xử lý ngoại tuyến hoặc giao tiếp thiết bị phần cứng:
        *   [YoloDetector.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/YoloDetector.cs): Quản lý ONNX Runtime nạp mô hình AI, tiền xử lý và chạy nhận diện YOLO.
        *   [ModelManagerService.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/ModelManagerService.cs): Theo dõi thư mục chứa file ONNX và nạp lại mô hình động bằng `FileSystemWatcher`.
        *   [CameraService.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/CameraService.cs): Điều khiển kết nối camera thô để chụp ảnh.
        *   [SettingsService.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/SettingsService.cs): Lưu trữ/tải cấu hình JSON.

---

# 2. Luồng Hoạt động Chính (Workflows)

### 📌 Luồng Khởi động hệ thống (System Initialization)
1. Ứng dụng chạy -> Khởi tạo [MainViewModel.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/ViewModels/MainViewModel.cs).
2. [MainViewModel.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/ViewModels/MainViewModel.cs) gọi [SettingsService.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/SettingsService.cs) nạp file cấu hình `settings.json`.
3. Khởi tạo [ModelManagerService.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/ModelManagerService.cs). Service này đọc file `Models/active.json` để xác định đường dẫn mô hình ONNX hiện tại và truyền cho [YoloDetector.cs](file:///c:/Users/l-duoc/OneDrive - Cong ty TNHH Adtec Plasma Technology Vietnam/Alpha/Software/CounterVision/Services/YoloDetector.cs) nạp vào bộ nhớ.
4. Lắng nghe sự kiện đổi model động từ file `active.json`.
5. Tạo các ViewModels con (`DashboardViewModel`, `ModelViewModel`, `SettingsViewModel`) và trỏ giao diện chính vào Dashboard.

### 📌 Luồng Nhận diện Lỗi (Inference Flow)
1. Người dùng kết nối camera và bấm nút **Start (Capture)** trên Dashboard.
2. `DashboardViewModel` ra lệnh chụp ảnh từ `CameraService` bất đồng bộ -> trả về byte array hình ảnh thô.
3. Gửi byte array hình ảnh thô này sang bộ xử lý `YoloDetector` (chạy trên luồng phụ để tránh treo UI).
4. `YoloDetector` thực thi:
    * Chuyển đổi định dạng ảnh và scale về kích thước `640x640`.
    * Chạy suy luận ONNX Runtime sinh tọa độ bounding box lỗi.
    * Chạy thuật toán lọc trùng NMS (Non-Maximum Suppression).
    * Vẽ hộp lỗi (đỏ/vàng) đè lên ảnh gốc bằng OpenCVSharp và trả về ảnh kết quả kèm thông tin kết quả `DetectionResult`.
5. Dashboard cập nhật ảnh kết quả đã vẽ box lỗi lên giao diện, thực hiện lưu trữ file ảnh vào thư mục cục bộ (vào thư mục con `NG` hoặc `OK` tùy thuộc số lượng lỗi phát hiện được) và tự động chuyển sang trang hiển thị kết quả chi tiết.
 
---

# 3. Vị trí thêm file `best.onnx`

Hệ thống được thiết kế để tìm kiếm mô hình trong thư mục chạy ứng dụng chính (`bin/Debug/net8.0-windows/Models`). Bạn cần đặt mô hình tại đây:

1.  **Vị trí khuyên dùng để cập nhật nhanh:**
    Hãy copy file `best.onnx` trực tiếp vào thư mục runtime sau khi build:
    ```text
    [Thư mục chứa dự án]\CounterVision\bin\Debug\net8.0-windows\Models\best.onnx
    ```
    *(Hoặc bạn có thể tạo thư mục sản phẩm bên trong đó, ví dụ: `...\Models\ProductA\best.onnx`)*
2.  **Cách kích hoạt mô hình mới trên Giao diện:**
    *   Mở ứng dụng của bạn, chọn trang **🤖 Model** trên thanh menu bên trái.
    *   Nhấp nút **🔄 Refresh** để hệ thống quét lại thư mục Models. Bạn sẽ thấy dòng chứa tên mô hình `best.onnx` xuất hiện trong bảng.
    *   Tích chọn hộp kiểm **[✓]** bên cạnh file mô hình `best.onnx`.
    *   Nhấn **💾 Save Selection** (Lưu lựa chọn). 
    *   Hệ thống sẽ tự động cập nhật file `active.json` và bộ đọc ghi ngầm `FileSystemWatcher` sẽ tự động tải lại mô hình này vào bộ xử lý AI cục bộ ngay tức khắc mà không cần tắt ứng dụng!