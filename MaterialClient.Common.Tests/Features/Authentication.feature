Feature: 用户登录和会话管理
    作为一个用户
    我想要使用账号和密码登录系统
    以便能够使用系统的各项功能

Background:
    Given 系统已完成授权激活
    And 授权未过期
    And 已初始化通用测试数据

Scenario: 使用正确的用户名和密码登录成功
    Given 用户在登录页面
    When 用户输入用户名 "testuser" 和密码 "Test@123"
    And 用户点击登录按钮
    Then 登录应该成功
    And 用户会话应该被创建
    And 用户应该进入主界面

Scenario: 使用错误的密码登录失败
    Given 用户在登录页面
    When 用户输入用户名 "testuser" 和密码 "wrongpassword"
    And 用户点击登录按钮
    Then 登录应该失败
    And 应该显示错误消息 "用户名或密码错误"
    And 用户会话不应该被创建

Scenario: 使用空用户名登录失败
    Given 用户在登录页面
    When 用户输入用户名 "" 和密码 "Test@123"
    And 用户点击登录按钮
    Then 应该显示验证错误 "用户名不能为空"
    And 不应该调用登录API

Scenario: 使用空密码登录失败
    Given 用户在登录页面
    When 用户输入用户名 "testuser" 和密码 ""
    And 用户点击登录按钮
    Then 应该显示验证错误 "密码不能为空"
    And 不应该调用登录API

Scenario: 勾选"记住密码"后登录成功保存凭证
    Given 用户在登录页面
    When 用户输入用户名 "testuser" 和密码 "Test@123"
    And 用户勾选"记住密码"选项
    And 用户点击登录按钮
    Then 登录应该成功
    And 用户凭证应该被加密保存到数据库
    And 下次启动时应该自动填充用户名和密码

Scenario: 不勾选"记住密码"后登录成功不保存凭证
    Given 用户在登录页面
    And 之前有保存的凭证
    When 用户输入用户名 "testuser" 和密码 "Test@123"
    And 用户不勾选"记住密码"选项
    And 用户点击登录按钮
    Then 登录应该成功
    And 之前保存的凭证应该被清除

Scenario: 登录失败后清除保存的凭证
    Given 用户在登录页面
    And 有保存的凭证
    When 用户输入用户名 "testuser" 和密码 "wrongpassword"
    And 用户点击登录按钮
    Then 登录应该失败
    And 保存的凭证应该被清除

Scenario: 网络连接失败时显示友好错误消息
    Given 用户在登录页面
    And 网络连接不可用
    When 用户输入用户名 "testuser" 和密码 "Test@123"
    And 用户点击登录按钮
    Then 应该显示网络错误消息 "网络连接失败，请检查网络设置"
    And 应该显示重试按钮

Scenario: 会话管理 - 检测活跃会话
    Given 用户已成功登录
    And 用户会话存在于数据库
    When 检查是否有活跃会话
    Then 应该返回true
    And 应该返回有效的会话信息

Scenario: 会话过期检测 - 超过24小时无活动
    Given 用户已成功登录
    And 用户会话的最后活动时间是25小时前
    When 检查是否有活跃会话
    Then 应该返回false
    And 会话应该被自动清除

Scenario: 用户登出
    Given 用户已成功登录
    And 用户会话存在
    When 用户点击登出
    Then 用户会话应该被删除
    And 用户应该返回登录页面

Scenario: 自动填充保存的凭证
    Given 有保存的用户凭证在数据库
    When 登录窗口被打开
    Then 用户名应该被自动填充
    And 密码应该被自动填充
    And "记住密码"选项应该被勾选

Scenario: 密码加密存储
    Given 用户在登录页面
    When 用户输入用户名 "testuser" 和密码 "Test@123"
    And 用户勾选"记住密码"
    And 用户点击登录按钮
    Then 密码应该使用AES-256-CBC加密
    And 加密后的密码应该存储在数据库
    And 原始密码不应该以明文形式存储

Scenario: 密码解密和验证
    Given 有加密保存的凭证
    When 加载保存的凭证
    Then 密码应该被正确解密
    And 解密后的密码应该与原始密码一致

