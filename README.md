# Cut2DX - 2D Cutting Optimization Library (.NET 10 / C#)

> **Thư viện tối ưu hóa cắt tấm 2 chiều (2D Cutting Stock Problem - 2DCSP)** viết thuần bằng C# (.NET 10), không phụ thuộc COM, không cần Windows Registry, tốc độ xử lý cao với các giải thuật tối ưu: **Guillotine**, **Nested**, và **Multi-Stages**. Dự án bao gồm Core Engine, Ứng dụng Desktop WinForms và Ứng dụng Web ASP.NET Core với giao diện vẽ sơ đồ trực quan.

---

## 📑 Mục lục
1. [Giới thiệu tổng quan](#1-giới-thiệu-tổng-quan)
2. [Cấu trúc dự án](#2-cấu-trúc-dự-án)
3. [Tra cứu toàn bộ API (API Reference)](#3-tra-cứu-toàn-bộ-api-api-reference)
   - [3.1. Bảng Thuộc tính (Properties)](#31-bảng-thuộc-tính-properties)
   - [3.2. Bảng Phương thức (Methods)](#32-bảng-phương-thức-methods)
4. [Các giải thuật cốt lõi](#4-các-giải-thuật-cốt-lõi)
5. [Quy tắc kỹ thuật quan trọng](#5-quy-tắc-kỹ-thuật-quan-trọng)
   - [Hướng vân gỗ (Grain Direction)](#hướng-vân-gỗ-grain-direction)
   - [Bề dày mạch cắt (BladeWidth / Kerf)](#bề-dày-mạch-cắt-bladewidth--kerf)
   - [Cắt tỉa mép (Trim Margins)](#cắt-tỉa-mép-trim-margins)
6. [Hướng dẫn cài đặt & Khởi chạy](#6-hướng-dẫn-cài-đặt--khởi-chạy)
   - [Ứng dụng Web (ASP.NET Core)](#ứng-dụng-web-optimal2dxweb)
   - [Ứng dụng WinForms](#ứng-dụng-winforms-optimal2dxwinforms)
7. [Ví dụ mã nguồn tích hợp (Quick Start)](#7-ví-dụ-mã-nguồn-tích-hợp-quick-start)

---

## 1. Giới thiệu tổng quan

`Cut2DX` giải quyết bài toán bố trí các chi tiết hình chữ nhật (Demand Pieces) lên các tấm nguyên liệu hình chữ nhật có sẵn trong kho (Repository Pieces) sao cho:
- **Tối đa hóa tỷ lệ sử dụng nguyên liệu** (Surface Covered %).
- **Tối thiểu hóa phế liệu hao phí** (Waste Area).
- **Hỗ trợ cưa xẻ công nghiệp**: Cưa suốt mép (Guillotine cut), bảo toàn hướng thớ vân gỗ (Grain Direction), bù hao hụt mạch cưa (Blade kerf) và xén mép phôi lỗi (Trim margins).

---

## 2. Cấu trúc dự án

```text
Cut2dx/
├── Optimal2DX_CSharp/
│   ├── Optimal2DX.Core/            # Thư viện thuật toán cốt lõi (.NET Class Library)
│   │   ├── Optimal2DX.cs          # Lớp chính triển khai API Cut2DX
│   │   └── Native/                # Quản lý bộ nhớ và cấu trúc dữ liệu tối ưu
│   └── Optimal2DX.WinForms/        # Ứng dụng Desktop WinForms (.NET 10-windows)
│       ├── FormMain.cs            # Giao diện chính, canvas vẽ sơ đồ GDI+
│       └── Models/                # Model gom nhóm sơ đồ và quản lý chi tiết
├── Optimal2DX.Web/                 # Ứng dụng Web hiện đại (.NET 10 ASP.NET Core)
│   ├── Controllers/               # API Controllers (Optimize, Import/Export Excel)
│   ├── Models/                    # DTOs cho bài toán tối ưu
│   └── wwwroot/                   # Giao diện Web HTML5 Canvas, Dark/Light Mode,
│                                  # Hỗ trợ dán dữ liệu đa dòng & Import Excel
├── HuongDanSuDung_Optimal2DX.html  # Tài liệu kỹ thuật định dạng HTML chi tiết
└── README.md                      # Tài liệu tra cứu API & hướng dẫn sử dụng
```

---

## 3. Tra cứu toàn bộ API (API Reference)

Lớp điều khiển chính: `Optimal2DX.Core.Optimal2DX`

### 3.1. Bảng Thuộc tính (Properties)

| Tên thuộc tính | Kiểu dữ liệu | Get/Set | Mô tả chi tiết |
| :--- | :---: | :---: | :--- |
| `NumberOfDemandPieces` | `int` | `get; set;` | Số lượng chi tiết cần cắt. Thiết lập giá trị này sẽ khởi tạo/cấp phát mảng dữ liệu chi tiết. |
| `NumberOfRepositoryPieces` | `int` | `get; set;` | Số lượng tấm nguyên liệu trong kho. Thiết lập giá trị này sẽ cấp phát mảng tấm kho. |
| `BladeWidth` | `double` | `get; set;` | Bề dày của mạch cưa / tia cắt (*kerf*). Cho phép số lẻ và tự động làm tròn đến 1 chữ số thập phân (ví dụ: `3.2 mm`). Mặc định: `0`. |
| `OptimizationLevel` | `int` | `get; set;` | Cấp độ tìm kiếm di truyền (`1` đến `100`). Càng cao thuật toán tìm nghiệm càng kỹ, thời gian chạy tăng dần. Mặc định: `50`. |
| `RandomSeed` | `int` | `get; set;` | Seed khởi tạo bộ sinh ngẫu nhiên. Giữ nguyên seed sẽ cho ra kết quả sắp xếp giống hệt nhau. Mặc định: `1`. |
| `MinimalUsefulSize` | `int` | `get; set;` | Kích thước cạnh tối thiểu để một mảnh thừa được coi là *"phế liệu có ích / tái sử dụng"*. Mảnh nhỏ hơn sẽ tính là *"phế phẩm vụn"* (*Garbage*). |
| `MaximalCutLength` | `int` | `get; set;` | Giới hạn chiều dài tối đa của một đường cắt xẻ (phù hợp với giới hạn hành trình của máy cưa bàn trượt). `0` = không giới hạn. |
| `FirstDirectionMultiStages` | `int` | `get; set;` | Hướng cắt đầu tiên trong chế độ Multi-Stages: `0` = Cắt ngang thành dải trước (*Horizontal strips*), `1` = Cắt dọc trước (*Vertical strips*). |
| `CutAllStripsFirst` | `int` | `get; set;` | Ưu tiên cắt toàn bộ các dải chính trước khi cắt chia nhỏ các chi tiết bên trong: `1` = Bật, `0` = Tắt. |
| `TotalNumberOfUtilizedRepositoryPieces` | `int` | `get;` | Tổng số tấm nguyên liệu kho thực tế đã được thuật toán sử dụng sau khi tối ưu. |
| `TotalNumberOfUtilizedDemandPieces` | `int` | `get;` | Tổng số chi tiết cần cắt đã được xếp thành công lên các tấm phôi. |
| `TotalWaste` | `double` | `get;` | Tổng diện tích phế liệu hao phí trên tất cả các tấm đã dùng. |
| `Version` | `string` | `get;` | Phiên bản của thư viện (ví dụ: `"4.0.0 (C# Edition)"`). |

---

### 3.2. Bảng Phương thức (Methods)

#### `SetDemandPiece`
Khai báo kích thước và thuộc tính của một chi tiết cần cắt.
```csharp
void SetDemandPiece(int index, long length, long width, int canRotate, long externalId, int priority)
```
- **`index`**: Thứ tự chi tiết (từ `0` đến `NumberOfDemandPieces - 1`).
- **`length`**, **`width`**: Chiều dài và chiều rộng chi tiết (số nguyên > 0).
- **`canRotate`**:
  - `1`: Cho phép xoay 90° để tối ưu diện tích.
  - `0`: Khóa góc xoay, giữ nguyên hướng vân thớ gỗ.
- **`externalId`**: Mã định danh tùy chọn để theo dõi chi tiết trong hệ thống của bạn.
- **`priority`**: Mức độ ưu tiên cắt (`0` = ưu tiên cao nhất).

---

#### `SetRepositoryPiece`
Khai báo kích thước và các thông số mép biên của một tấm phôi trong kho.
```csharp
void SetRepositoryPiece(int index, long length, long width, long trimTop, long trimLeft, long trimBottom, long trimRight, long externalId, int priority, int numHoles)
```
- **`index`**: Thứ tự tấm kho (từ `0` đến `NumberOfRepositoryPieces - 1`).
- **`length`**, **`width`**: Kích thước tấm phôi thô trong kho.
- **`trimTop`**, **`trimLeft`**, **`trimBottom`**, **`trimRight`**: Bề rộng viền mép biên bị lỗi cần tỉa bỏ trước khi cắt.
- **`externalId`**: Mã định danh tấm kho.
- **`priority`**: Mức độ ưu tiên sử dụng tấm (`0` = ưu tiên dùng trước).
- **`numHoles`**: Số lượng vùng khuyết tật/lỗ thủng sẵn có trên tấm (thông thường để `0`).

---

#### Các hàm kích hoạt giải thuật tối ưu
```csharp
int StartGuillotine()
int StartNested()
int StartMultiStages()
```
- **`StartGuillotine()`**: Tối ưu theo phương pháp cưa xẻ thẳng suốt mép (máy cưa bàn trượt, máy cưa panel saw).
- **`StartNested()`**: Tối ưu theo phương pháp xếp lồng tự do 2D (máy cắt CNC Router, Laser, Plasma, Tia nước).
- **`StartMultiStages()`**: Tối ưu theo giải thuật cắt Guillotine phân tầng nhiều giai đoạn (cắt dải trước rồi chia nhỏ).
- **Giá trị trả về**: Mã trạng thái (`0` = thành công, khác 0 = có lỗi hoặc dừng sớm).

---

#### Các hàm trích xuất kết quả

##### 1. `SurfaceCovered`
```csharp
void SurfaceCovered(int sheetIndex, out double surfCovered, out int repIndex, out long repId)
```
Lấy tỷ lệ diện tích che phủ hữu ích (`0%` - `100%`) trên tấm có thứ tự `sheetIndex`.
- `out surfCovered`: Tỷ lệ phần trăm diện tích có ích đã xếp chi tiết.
- `out repIndex`: Chỉ số tấm kho tương ứng đã được dùng.
- `out repId`: Mã định danh `externalId` của tấm kho.

##### 2. `GetCut`
```csharp
void GetCut(int sheetIndex, int cutIndex, out long tx, out long ty, out long bx, out long by, out long thick, out int repIndex, out long repId)
```
Lấy tọa độ đường cắt cưa thứ `cutIndex` trên tấm:
- `(tx, ty)`: Tọa độ điểm đầu của nhát cắt.
- `(bx, by)`: Tọa độ điểm cuối của nhát cắt.
- `thick`: Độ dày mạch cưa (bằng `BladeWidth`).

##### 3. `GetUtilizedDemandPiece`
```csharp
void GetUtilizedDemandPiece(int sheetIndex, int pieceIndex, out long tx, out long ty, out long bx, out long by, out int rot, out int repIndex, out int demIdx, out long repId, out long demId)
```
Lấy tọa độ hình chữ nhật chứa chi tiết đã được xếp lên tấm:
- `(tx, ty)`: Tọa độ góc trên - trái.
- `(bx, by)`: Tọa độ góc dưới - phải.
- `rot`: `1` nếu chi tiết bị xoay 90°, `0` nếu giữ nguyên hướng ban đầu.
- `demIdx`, `demId`: Chỉ số và mã định danh của chi tiết tương ứng.

##### 4. `GetWastePiece`
```csharp
void GetWastePiece(int sheetIndex, int wasteIndex, out long tx, out long ty, out long bx, out long by, out int repIndex, out long repId)
```
Lấy tọa độ hình chữ nhật của mảnh phế liệu dư thừa thứ `wasteIndex` trên tấm để phục vụ tái sử dụng hoặc tính toán phế phẩm.

##### 5. `SaveToFile`
```csharp
void SaveToFile(string fileName, int saveInput, int saveParams, int saveResults)
```
Lưu toàn bộ bài toán, thông số cấu hình và kết quả tính toán ra file văn bản định dạng chuẩn Cut2DX (`.txt`).

##### 6. `Stop`
```csharp
void Stop()
```
Dừng sớm tiến trình tối ưu hóa. Thuật toán sẽ lập tức kết thúc và trả về phương án tốt nhất đã tìm thấy được tại thời điểm đó.

---

## 4. Các giải thuật cốt lõi

| Đặc điểm | Guillotine (Cắt suốt mép) | Nested (Xếp tự do CNC) | Multi-Stages (Cắt phân tầng) |
| :--- | :--- | :--- | :--- |
| **Bản chất đường cắt** | Nhát cắt thẳng chạy suốt từ mép này sang mép đối diện của tấm phôi. | Chi tiết bố trí tự do mọi vị trí, đường cắt theo chu vi từng chi tiết. | Chia tấm thành các dải song song (strips), sau đó cắt ngang từng dải. |
| **Thiết bị gia công** | Máy cưa bàn trượt, Máy cưa Panel Saw công nghiệp, Máy cắt tôn thủy lực. | Máy cắt CNC Router nội thất, Laser CO2/Fiber, Plasma, Cắt tia nước. | Máy cưa tự động nhiều cụm lưỡi, dây chuyền xẻ gỗ công nghiệp. |
| **Tỷ lệ tận dụng** | Rất tốt (85% - 94%). | Cực đại (90% - 98%). | Tối ưu theo dải (82% - 92%). |
| **Khả năng thi công** | Thao tác cưa đơn giản, nhanh chóng, thợ cưa thủ công dễ làm theo. | Bắt buộc chạy bằng máy gia công CNC điều khiển qua G-code. | Thao tác cưa hàng loạt theo dải năng suất rất cao. |

---

## 5. Quy tắc kỹ thuật quan trọng

### Hướng vân gỗ (Grain Direction)
Để đảm bảo tính thẩm mỹ của sản phẩm nội thất gỗ (ván MDF chống ẩm phủ Melamine, Veneer, Laminate, Gỗ sồi ghép,...):
1. **Tấm phôi kho**: Khai báo kích thước với chiều `Length` luôn song song với chiều thớ vân gỗ.
2. **Chi tiết cần cắt**: Cạnh nào bạn muốn song song với chiều thớ gỗ thì khai báo vào tham số `length`, cạnh còn lại vuông góc đưa vào `width`.
3. **Khóa góc xoay**: Thiết lập `canRotate = 0` trong `SetDemandPiece` để thuật toán không xoay góc 90°, đảm bảo 100% chi tiết cắt ra đúng chiều vân.

### Bề dày mạch cắt (BladeWidth / Kerf)
Lưỡi cưa đĩa kim loại thường dày từ `2.8 mm - 4.2 mm` (ví dụ: `3.2 mm` hoặc `3.5 mm`). Khi cưa một nhát, một lượng phôi sẽ bị hao hụt thành mùn cưa:
- Thiết lập `BladeWidth = 3.2` (hỗ trợ số thực/số lẻ và tự động làm tròn đến 1 chữ số thập phân).
- Thuật toán sẽ tự động cộng bù khoảng cách mạch cắt giữa các chi tiết liền kề, giữ cho kích thước thành phẩm chuẩn xác tuyệt đối.

### Cắt tỉa mép (Trim Margins)
Tấm ván công nghiệp khi vận chuyển thường bị nứt mép hoặc dập góc:
- Khai báo các giá trị `trimTop`, `trimLeft`, `trimBottom`, `trimRight` trong hàm `SetRepositoryPiece`.
- Thuật toán tự động loại bỏ các dải biên bị lỗi này và bố trí chi tiết bắt đầu từ vị trí sạch `(trimLeft, trimTop)`.

---

## 6. Hướng dẫn cài đặt & Khởi chạy

### Yêu cầu hệ thống
- Hệ điều hành: Windows 10/11, Linux hoặc macOS (đối với Core và Web App).
- SDK: [.NET 10.0 SDK](https://dotnet.microsoft.com/) trở lên.

### Ứng dụng Web (Optimal2DX.Web)
Ứng dụng Web hiện đại, responsive, hỗ trợ:
- Dán nhanh danh sách chi tiết từ Excel / Clipboard.
- Import trực tiếp file Excel (`.xlsx`, `.xls`).
- Xuất file Excel kết quả thống kê.
- Vẽ trực quan sơ đồ cắt trên HTML5 Canvas.
- Tự động gom nhóm các sơ đồ trùng khớp (Pattern Grouping).

```bash
# Di chuyển vào thư mục Web
cd Optimal2DX.Web

# Chạy ứng dụng web
dotnet run
```
Truy cập trình duyệt tại: `http://localhost:5103`

### Ứng dụng WinForms (Optimal2DX.WinForms)
Ứng dụng đồ họa Desktop dành cho Windows với khung vẽ GDI+ trực quan, hỗ trợ mô phỏng vân gỗ, gom nhóm sơ đồ giống nhau và xuất báo cáo:
```bash
dotnet run --project Optimal2DX_CSharp/Optimal2DX.WinForms/Optimal2DX.WinForms.csproj
```

---

## 7. Ví dụ mã nguồn tích hợp (Quick Start)

Dưới đây là ví dụ hoàn chỉnh khởi tạo bài toán và chạy giải thuật Guillotine bằng C#:

```csharp
using System;
using Optimal2DX.Core;

class Program
{
    static void Main()
    {
        Optimal2DX opt = new Optimal2DX();

        // 1. Cấu hình thông số cắt
        opt.BladeWidth = 3.2;         // Bề dày mạch cưa: 3.2 mm
        opt.OptimizationLevel = 80;    // Mức độ tối ưu: 80%

        // 2. Khai báo tấm phôi nguyên liệu trong kho (2440 x 1220 mm)
        opt.NumberOfRepositoryPieces = 2;
        // SetRepositoryPiece(index, length, width, trimTop, trimLeft, trimBottom, trimRight, extId, priority, holes)
        opt.SetRepositoryPiece(0, 2440, 1220, 10, 10, 10, 10, 101, 0, 0);
        opt.SetRepositoryPiece(1, 2440, 1220, 10, 10, 10, 10, 102, 0, 0);

        // 3. Khai báo các chi tiết cần cắt
        opt.NumberOfDemandPieces = 3;
        // SetDemandPiece(index, length, width, canRotate, extId, priority)
        opt.SetDemandPiece(0, 800, 600, 1, 1, 0);   // Cho phép xoay
        opt.SetDemandPiece(1, 1200, 400, 0, 2, 0);  // Khóa hướng vân gỗ
        opt.SetDemandPiece(2, 600, 400, 1, 3, 0);

        // 4. Kích hoạt tối ưu Guillotine
        int status = opt.StartGuillotine();
        Console.WriteLine($"Trạng thái tối ưu: {status} (0 = Thành công)");

        // 5. Đọc kết quả
        Console.WriteLine($"Số tấm kho đã dùng: {opt.TotalNumberOfUtilizedRepositoryPieces}");
        Console.WriteLine($"Số chi tiết đã xếp: {opt.TotalNumberOfUtilizedDemandPieces}");

        for (int s = 0; s < opt.TotalNumberOfUtilizedRepositoryPieces; s++)
        {
            opt.SurfaceCovered(s, out double coverage, out int repIdx, out long repId);
            Console.WriteLine($"Tấm #{s + 1} (Kho ID: {repId}) - Tỷ lệ sử dụng: {coverage:F1}%");
        }
    }
}
```

---

## 📄 Bản quyền
Dự án được phát triển và tối ưu hóa trên nền tảng .NET 10. Mọi quyền được bảo lưu.
