using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace R.DescriptionModelGenerator;

/// <summary>
/// 测试API控制器
/// </summary>
/// <remarks>
/// 用于演示API描述生成功能，包括XML注释的提取和处理
/// </remarks>
[ApiController]
[Route("api/[controller]/[action]")]
public class TestController : ControllerBase
{
    public TestController()
    {
        
    }

    /// <summary>
    /// 获取测试参数（可空）
    /// </summary>
    /// <param name="param">可选的字符串参数，用于测试null值处理</param>
    /// <returns>返回格式化后的测试字符串</returns>
    /// <remarks>
    /// 这是一个简单的GET请求示例，演示可空参数的处理和XML注释功能
    /// </remarks>
    [HttpGet]
    public string GetTestParamNull(string? param)
    {
        return 1+"";
    }

    /// <summary>
    /// 获取测试模型数据
    /// </summary>
    /// <param name="input">输入模型参数，包含创建测试模型所需的所有信息</param>
    /// <returns>返回创建的测试模型实例，包含所有必要的属性信息</returns>
    /// <remarks>
    /// 这是一个异步POST请求示例，演示复杂对象的处理和返回，
    /// 同时展示如何通过XML注释为参数和返回值提供详细说明
    /// </remarks>
    [HttpPost]
    public async Task<TestModel> GetTestModel(ModelInput input)
    {
        await Task.Delay(1);
        return new TestModel();
    }

    /// <summary>
    /// 处理测试模型的空值情况
    /// </summary>
    /// <param name="param">测试模型参数，用于验证空值处理逻辑</param>
    /// <returns>返回处理结果的字符串表示</returns>
    /// <remarks>
    /// 演示如何处理复杂对象参数，包括空值检查和异常处理
    /// </remarks>
    [HttpGet]
    public string GetTestModelNull(TestModel param)
    {
        return 1+"";
    }

    /// <summary>
    /// 测试数据模型
    /// </summary>
    /// <remarks>
    /// 包含多种类型属性的测试模型，用于演示类型描述生成和XML注释提取功能
    /// </remarks>
    public class TestModel
    {
        /// <summary>
        /// 类型标识符
        /// </summary>
        /// <remarks>
        /// 可空的字符串属性，用于标识对象的具体类型或分类
        /// </remarks>
        public string? Type { get; set; }

        /// <summary>
        /// 实体名称
        /// </summary>
        /// <remarks>
        /// 必填的名称字段，不能为空，是该实体的主要标识信息
        /// </remarks>
        [Required]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 子模型对象
        /// </summary>
        /// <remarks>
        /// 嵌套的子对象，用于演示复杂类型引用和对象关系处理
        /// </remarks>
        public ChildeModel ChildeModel { get; set; } = new();
    }

    /// <summary>
    /// 子模型类
    /// </summary>
    /// <remarks>
    /// 简单的子对象模型，用于演示对象嵌套关系和属性继承
    /// </remarks>
    public class ChildeModel
    {
        /// <summary>
        /// 子对象名称
        /// </summary>
        /// <remarks>
        /// 子对象的可空名称属性，用于标识子对象的具体信息
        /// </remarks>
        public string? Name { get; set; }
    }
}
