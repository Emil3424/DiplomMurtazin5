using Microsoft.VisualStudio.TestTools.UnitTesting;
using DiplomMurtazin.Core;

namespace DiplomMurtazin.UnitTests
{
    [TestClass]
    public class PhotoHelperTests
    {
        [TestMethod]
        public void GetFullPath_ReturnsAbsolutePath()
        {
            string result = PhotoHelper.GetFullPath("EmployeePhotos/test.jpg");

            Assert.IsTrue(System.IO.Path.IsPathRooted(result));
        }
    }
}
