namespace OnlineClearanceSystem.Models
{
    public class StudentProfileViewModel
    {
        public string StudentId     { get; set; } = "";
        public string FullName      { get; set; } = "";
        public string FirstName     { get; set; } = "";
        public string MiddleInitial { get; set; } = "";
        public string LastName      { get; set; } = "";
        public string Suffix        { get; set; } = "";
        public string Course        { get; set; } = "BSIT";
        public string YearLevel     { get; set; } = "2nd Year";
        public string Section       { get; set; } = "2A";
        public string Username      { get; set; } = "";
        public string Password      { get; set; } = "";
    }
}