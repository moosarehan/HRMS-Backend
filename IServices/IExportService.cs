using HRMS_BACKEND.Dto.Attendance;

namespace HRMS_BACKEND.IServices;

public interface IExportService
{
    byte[] ToTxt(List<AttendanceExportDto> records);
    byte[] ToPdf(List<AttendanceExportDto> records);
    byte[] ToDocx(List<AttendanceExportDto> records);
}