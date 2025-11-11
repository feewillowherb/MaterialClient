Feature: 软件授权验证
    作为一个新用户
    我想输入授权码激活软件
    以便能够使用系统功能

Scenario: 首次使用输入有效授权码
    Given 本地数据库中没有授权信息
    When 用户启动应用程序
    Then 系统显示授权码输入窗口
    When 用户输入有效授权码 "test-auth-code-123"
    And 点击确认按钮
    Then 系统调用基础平台API验证授权码
    And 保存授权信息到数据库
    And 进入登录页面

Scenario: 输入无效授权码
    Given 本地数据库中没有授权信息
    When 用户在授权窗口输入无效授权码 "invalid-code"
    And 点击确认按钮
    Then 系统显示错误提示 "授权码无效"
    And 不进入登录页面

Scenario: 授权已过期
    Given 本地数据库中存在已过期的授权信息
    When 用户启动应用程序
    Then 系统检测到授权已过期
    And 显示授权码输入窗口

Scenario: 有效授权存在时跳过授权窗口
    Given 本地数据库中存在有效的授权信息
    When 用户启动应用程序
    Then 系统跳过授权码输入窗口
    And 直接进入登录页面

Scenario: 关闭授权窗口
    Given 本地数据库中没有授权信息
    And 系统显示授权码输入窗口
    When 用户关闭授权窗口
    Then 应用程序退出

