using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace R.DescriptionModelGenerator;

[ApiController]
[Route("api/[controller]/[action]")]
public class TestController : ControllerBase
{
    public TestController()
    {
        
    }
    [HttpGet]
    public string GetTestParamNull(string? param)
    {
        return 1+"";
    }

    [HttpPost]
    public async Task<TestModel> GetTestModel(ModelInput input)
    {
        await Task.Delay(1);
        return new TestModel();
    }
    [HttpGet]
    public string GetTestModelNull(TestModel param)
    {
        return 1+"";
    }
    public class TestModel
    {
        public string? Type { get; set; }
        [Required]
        public string Name { get; set; }
        public ChildeModel ChildeModel { get; set; }
    }

    public class ChildeModel
    {
        public string? Name { get; set; }
    }
}