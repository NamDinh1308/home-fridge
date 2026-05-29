# TÀI LIỆU CẤU TRÚC VÀ LOGIC NGHIỆP VỤ - HỆ THỐNG HOME'S FRIDGE

Tài liệu này mô tả chi tiết cấu trúc thư mục dự án, mô hình dữ liệu (Database), các thuật toán cốt lõi, logic nghiệp vụ, và các công thức tính toán áp dụng trong hệ thống quản lý thực phẩm gia đình thông minh **Home's Fridge** (ASP.NET Core 8.0 MVC + Entity Framework Core + SQLite + TailwindCSS).

---

## 1. CẤU TRÚC THƯ MỤC DỰ ÁN

Dự án được xây dựng theo kiến trúc MVC truyền thống kết hợp với mô hình Service Layer để cô lập logic nghiệp vụ (Business Logic).

```text
HomeFridgev1-BK1/
├── package.json & package-lock.json      # Quản lý thư viện frontend (Tailwind CSS CLI v4)
├── HomeFridgev1/                         # Thư mục mã nguồn chính của ứng dụng
│   ├── Areas/                            # Khu vực dành cho phân quyền (Identity mặc định)
│   ├── Controllers/                      # Nơi định nghĩa các endpoint và xử lý điều hướng View
│   ├── Data/                             # Chứa ApplicationDbContext và cấu hình Entity Framework
│   ├── Migrations/                       # Lịch sử thay đổi và cập nhật cấu trúc cơ sở dữ liệu SQLite
│   ├── Models/                           # Định nghĩa các Model dữ liệu
│   │   ├── Entities/                     # Các thực thể ánh xạ trực tiếp xuống Database
│   │   ├── Enums/                        # Định nghĩa các trạng thái (FoodStatus, MemberRole...)
│   │   ├── Settings/                     # Cấu hình hệ thống (EmailProviderSettings...)
│   │   └── ViewModels/                   # Các Model phục vụ hiển thị dữ liệu ra màn hình View
│   ├── Services/                         # Tầng xử lý Logic nghiệp vụ (Service Layer)
│   │   ├── Interfaces/                   # Các Interface định nghĩa hợp đồng dịch vụ (Contracts)
│   │   ├── Email/                        # Dịch vụ gửi Email (tích hợp Resend API)
│   │   ├── Notification/                 # Dịch vụ quét định kỳ và gửi cảnh báo hết hạn, hết hàng
│   │   └── [Các Service khác]            # Quản lý Thực phẩm, Công thức, Thành viên...
│   ├── ViewComponents/                   # Các thành phần giao diện động (ví dụ: CurrentMember hiển thị profile hiện tại)
│   ├── Views/                            # Chứa giao diện Razor Pages (.cshtml)
│   ├── Properties/                       # Cấu hình khởi chạy dự án (launchSettings.json)
│   ├── wwwroot/                          # Tài nguyên tĩnh công khai (CSS, JS, hình ảnh thực phẩm, thư viện...)
│   │   └── css/                          # site.css, input.css, output.css (Tailwind)
│   ├── appsettings.json                  # File cấu hình kết nối DB, API Key gửi mail Resend...
│   └── Program.cs                        # File khởi tạo dịch vụ, cấu hình HTTP Pipeline và chạy app
└── homesfridge.db                        # File cơ sở dữ liệu SQLite thực tế
```

---

## 2. KIẾN TRÚC CƠ SỞ DỮ LIỆU (DATABASE ENTITIES)

Cơ sở dữ liệu SQLite bao gồm các thực thể chính được thiết kế để phục vụ việc quản lý đa hộ gia đình (Multi-Household) và phân quyền thành viên trong gia đình:

| Thực thể | Mô tả | Mối quan hệ chính |
| :--- | :--- | :--- |
| **Household** | Hộ gia đình. Thực thể trung tâm để phân tách dữ liệu giữa các nhà khác nhau. | 1-N với `MemberProfile`, `FoodItem`, `Recipe` |
| **HouseholdSettings** | Cấu hình riêng cho từng hộ gia đình (Thời gian quét cảnh báo, số ngày cảnh báo...). | 1-1 với `Household` |
| **MemberProfile** | Hồ sơ thành viên trong nhà (Bố, Mẹ, Con cái...) gắn với tài khoản Identity. | N-1 với `Household`, có quyền `Owner` (Chủ nhà) hoặc `Member` (Thành viên) |
| **FoodItem** | Thực phẩm có trong tủ lạnh. Lưu trữ số lượng, hạn sử dụng, vị trí bảo quản... | N-1 với `Household`, `Category`, `StorageLocation` |
| **Category** | Danh mục thực phẩm (Rau củ, Thịt cá, Sữa, Đồ uống...). | 1-N với `FoodItem` |
| **StorageLocation** | Vị trí lưu trữ vật lý (Ngăn mát, Ngăn đông, Kệ đồ khô...). | 1-N với `FoodItem` |
| **Recipe** | Công thức món ăn do gia đình tự thêm hoặc gợi ý. | 1-N với `RecipeIngredient` |
| **RecipeIngredient** | Chi tiết các nguyên liệu cần thiết cho một công thức. | N-1 với `Recipe` |
| **ShoppingItem** | Danh mục đồ cần đi chợ mua sắm. | N-1 với `Household`, `MemberProfile` |
| **FoodActivityLog** | Nhật ký thao tác thực phẩm (Thêm mới, Tiêu thụ khi nấu ăn, Lưu trữ...). | N-1 với `FoodItem`, `MemberProfile` |
| **NotificationLog** | Nhật ký gửi email thông báo (Hạn chế gửi trùng lặp). | N-1 với `Household` |

---

## 3. LOGIC NGHIỆP VỤ & THUẬT TOÁN CỐT LÕI

Hệ thống vận hành dựa trên các thuật toán thông minh hỗ trợ cuộc sống gia đình được lập trình tại tầng **Services**:

### 3.1. Thuật toán Tính toán Trạng thái Thực phẩm (Food Status Calculation)
Trạng thái của một mặt hàng thực phẩm được tính toán tự động dựa trên **số lượng hiện tại** và **số ngày còn lại đến hạn sử dụng (Expiry Date)** so với cấu hình cài đặt của hộ gia đình.

* **Đầu vào:**
  - $Q_{current}$ (CurrentQuantity): Số lượng thực phẩm hiện có.
  - $D_{expiry}$ (ExpiryDate): Ngày hết hạn của thực phẩm.
  - $D_{today}$ (Today): Ngày hiện tại của hệ thống.
  - $S_{urgent}$ (UrgentDaysBeforeExpiry): Số ngày giới hạn cấp bách (cấu hình trong `HouseholdSettings`).
  - $S_{warning}$ (WarningDaysBeforeExpiry): Số ngày giới hạn cảnh báo (cấu hình trong `HouseholdSettings`).
* **Trạng thái đầu ra (FoodStatus Enum):** `OutOfStock`, `Expired`, `Urgent`, `Warning`, `Normal`.

* **Logic tính toán cụ thể (`FoodStatusService.CalculateStatus`):**

$$\text{DaysRemaining} = D_{expiry} - D_{today}$$

```text
Nếu Q_current <= 0:
    Trả về Trạng thái = OutOfStock (Hết hàng)

Nếu DaysRemaining < 0:
    Trả về Trạng thái = Expired (Đã hết hạn)

Nếu DaysRemaining < S_urgent:
    Trả về Trạng thái = Urgent (Cấp bách - Sắp hết hạn rất gần)

Nếu DaysRemaining < S_warning:
    Trả về Trạng thái = Warning (Cảnh báo - Sắp hết hạn)

Ngược lại:
    Trả về Trạng thái = Normal (Bình thường - An toàn)
```

---

### 3.2. Thuật toán So khớp & Gợi ý Công thức Món ăn (Recipe Suggestion Algorithm)
Giúp người dùng trả lời câu hỏi *"Hôm nay tủ lạnh còn nguyên liệu này thì nấu món gì?"* bằng cách so khớp danh sách thực phẩm đang có trong tủ lạnh với danh sách nguyên liệu của các công thức nấu ăn.

* **Logic so khớp nguyên liệu:**
  Tên nguyên liệu trong công thức ($I_{name}$) được tìm kiếm trong danh sách thực phẩm thực tế ($F_{list}$) không phân biệt chữ hoa chữ thường và cho phép so khớp tương đối (Substring Match):
  
$$\text{Matched} \iff (F_{item}.Name \subset I_{name}) \lor (I_{name} \subset F_{item}.Name)$$

* **Xác định trạng thái của từng nguyên liệu trong công thức:**
  - **Match (Khớp hoàn toàn):** Tổng số lượng thực phẩm khớp trong tủ lạnh $\ge$ Số lượng công thức yêu cầu.
  - **Partial (Thiếu một phần):** Tổng số lượng thực phẩm khớp trong tủ lạnh $> 0$ nhưng $<$ Số lượng công thức yêu cầu.
  - **Missing (Thiếu hoàn toàn):** Không có thực phẩm nào khớp trong tủ lạnh (hoặc số lượng bằng 0).

* **Công thức tính tỷ lệ khớp (Match Percentage):**

$$\% \text{ Match} = \left( \frac{\text{Số nguyên liệu đạt trạng thái "Match"}}{\text{Tổng số nguyên liệu của công thức}} \right) \times 100$$

* **Logic Phân loại và Sắp xếp gợi ý hiển thị (`RecipeService.GetSuggestionsAsync`):**
  Các gợi ý món ăn được ưu tiên sắp xếp theo thứ tự thông minh giúp giải phóng nhanh thực phẩm sắp hỏng:
  1. **Nấu được ngay (`IsCookable = true`) & Có chứa nguyên liệu sắp hết hạn** (`Warning` hoặc `Urgent`): Được ưu tiên cao nhất để người dùng nấu ngay trước khi đồ bị hỏng.
  2. **Nấu được ngay (`IsCookable = true`) & Không có đồ sắp hết hạn**.
  3. **Thiếu một phần (`IsCookable = false`):** Sắp xếp giảm dần theo tỷ lệ phần trăm so khớp (`MatchPercentage` từ cao xuống thấp).
  4. **Thiếu hoàn toàn:** Xếp cuối cùng.

---

### 3.3. Thuật toán Tiêu dùng thực phẩm thông minh - FEFO (First Expired, First Out)
Khi người dùng bấm chọn **"Xác nhận nấu món ăn này"** (`CookRecipeAsync`), hệ thống sẽ tự động trừ kho nguyên liệu. Thay vì trừ ngẫu nhiên, hệ thống áp dụng chiến lược tiêu thụ **FEFO (Hạn ngắn dùng trước, hạn dài dùng sau)**:

* **Logic thực hiện:**
  1. Với mỗi nguyên liệu của công thức, lấy ra danh sách các lô thực phẩm khớp trong tủ lạnh.
  2. Sắp xếp danh sách này tăng dần theo ngày hết hạn: `.OrderBy(f => f.ExpiryDate)`.
  3. Tiến hành trừ số lượng cần thiết từ thực phẩm có hạn dùng ngắn nhất.
  4. Nếu số lượng của lô ngắn hạn không đủ, trừ tiếp sang lô có hạn dùng ngắn thứ hai, cứ thế cho đến khi trừ đủ số lượng yêu cầu ($Q_{required}$).
  5. Sau khi trừ, tính toán lại trạng thái (`CalculateStatus`) cho các mặt hàng bị trừ. Nếu số lượng về $0$, thiết lập mốc thời gian hết hàng `OutOfStockSince = DateTime.UtcNow`.
  6. Ghi lại nhật ký tiêu thụ vào bảng `FoodActivityLogs`.

---

### 3.4. Logic Tự động Đi chợ (Auto-Shopping List Integration)
Nếu người dùng muốn nấu một món ăn nhưng bị thiếu nguyên liệu, hệ thống cung cấp tính năng tự động chuẩn bị danh sách đi chợ (`AddMissingIngredientsToShoppingListAsync`):

$$\text{QuantityToBuy} = Q_{required} - Q_{available}$$

* **Logic xử lý:**
  1. Tìm kiếm xem nguyên liệu thiếu này đã tồn tại trong danh sách đi chợ (`ShoppingItems`) ở trạng thái **Chưa mua** (`IsPurchased = false`) hay chưa.
  2. Nếu **đã có**: Cộng dồn số lượng thiếu cần mua thêm vào mục hiện tại:
     $$\text{NewQuantity} = \text{CurrentShoppingQuantity} + \text{QuantityToBuy}$$
  3. Nếu **chưa có**: Tạo mới một bản ghi mua sắm với số lượng cần mua bằng đúng lượng còn thiếu.

---

### 3.5. Logic Tự động Lưu trữ Thực phẩm Hết hàng (Auto-Archive Rule)
Để tránh làm rác giao diện danh sách thực phẩm, hệ thống có cơ chế tự động ẩn và lưu trữ các thực phẩm đã hết hàng lâu ngày (`ApplyAutoHideRuleAsync`):

* **Logic thực hiện:**
  - Nếu hộ gia đình bật tính năng tự động ẩn (`EnableAutoHideOutOfStock = true`).
  - Quét các thực phẩm có trạng thái hết hàng ($Q \le 0$).
  - So sánh thời gian hết hàng:
    $$\text{DaysOutOfStock} = \text{DateTime.UtcNow} - \text{OutOfStockSince}$$
  - Nếu $\text{DaysOutOfStock} \ge S_{auto\_hide}$ (Số ngày tự động ẩn được cấu hình trong cài đặt hộ gia đình, ví dụ: 7 ngày):
    - Đánh dấu thực phẩm là đã lưu trữ: `IsArchived = true`, `ArchivedAt = DateTime.UtcNow`.
    - Ghi nhận nguyên nhân lưu trữ tự động và lưu vào nhật ký hoạt động.

---

### 3.6. Dịch vụ Quét Cảnh báo hàng ngày chạy ngầm (Daily Background Scan & Email Notifications)
Hệ thống sử dụng một `BackgroundService` chạy ngầm (`DailyFoodStatusScanWorker`) để kiểm tra hạn dùng thực phẩm mỗi ngày mà không cần người dùng truy cập.

* **Quy trình hoạt động:**
  1. Cứ mỗi **15 phút**, Worker thức dậy và quét qua danh sách các hộ gia đình đang hoạt động trong hệ thống.
  2. Lấy cấu hình thời gian kiểm tra hàng ngày của gia đình đó (ví dụ: `08:00 AM`).
  3. So sánh thời gian hiện tại của máy chủ. Nếu đã qua mốc giờ kiểm tra và hôm nay chưa chạy quét (`alreadyRunToday == false`):
     - Quét toàn bộ thực phẩm không bị lưu trữ (`!IsArchived`) của hộ gia đình đó.
     - Tính toán lại trạng thái mới cho từng món đồ.
     - Nếu phát hiện **sự thay đổi về trạng thái** (ví dụ từ `Normal` chuyển thành `Warning`, hoặc từ `Warning` thành `Urgent`, hoặc từ còn hàng thành `OutOfStock`):
       - Cập nhật trạng thái mới và ghi nhận thời điểm đổi trạng thái.
       - Kích hoạt dịch vụ thông báo gửi email (`FoodStatusNotificationService`) sử dụng nền tảng **Resend API**.
  4. Gửi email cảnh báo tổng hợp danh sách thực phẩm tương ứng đến tất cả các thành viên đăng ký nhận cảnh báo trong hộ gia đình.
  5. Đánh dấu hộ gia đình đã được quét thành công trong ngày hôm nay.

---

## 4. CHI TIẾT CẢNH BÁO BẰNG EMAIL (EMAIL NOTIFICATION LAYOUT)
Hệ thống email cảnh báo của **Home's Fridge** được thiết kế giao diện HTML hiện đại, hỗ trợ Responsive trên di động và tương thích tối đa với Gmail:
* **Màu sắc giao diện email động** thay đổi theo mức độ nghiêm trọng:
  - ⚠️ **Warning (Sắp hết hạn):** Tông màu vàng cam (`#f59e0b`).
  - 🚨 **Urgent (Cấp bách):** Tông màu đỏ tươi (`#ef4444`).
  - ❌ **Expired (Đã quá hạn):** Tông màu đỏ đậm (`#7f1d1d`).
  - 📦 **OutOfStock (Hết hàng):** Tông màu xanh dương (`#3b82f6`).
* **Tối ưu hình ảnh hiển thị**: Thay vì đính kèm hình ảnh trực tiếp (gây ra các chip đính kèm file ở cuối mail trên Gmail làm xấu email), hệ thống sử dụng một URL ảnh public đáng tin cậy đã được tải lên CDN của dự án (`https://files.catbox.moe/5xy3fe.jpg`) làm biểu ngữ (banner) hiển thị đầu trang, đảm bảo giao diện email trông cực kỳ chuyên nghiệp và cao cấp.
