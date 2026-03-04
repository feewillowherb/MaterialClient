# OpenSpec 验证规则

本文定义验证规则，确保在中文本地化过程中 OpenSpec 规范文档保持英文。

## OpenSpec 英文语言要求

**关键要求**：所有 OpenSpec 规范文档必须保持英文。此为 OpenSpec 系统的不可协商要求。

## 必须保持英文的文件

### 核心 OpenSpec 文件

```
openspec/specs/**/*.md           # 所有规范文档
openspec/changes/**/proposal.md   # 变更提案
openspec/changes/**/tasks.md      # 任务列表
openspec/changes/**/design.md     # 设计文档
openspec/changes/**/spec.md       # 变更规范
```

### 系统配置文件

```
openspec/AGENTS.md               # 代理配置
openspec/README.md               # 系统文档
CLAUDE.md                        # 系统指令
```

## 验证规则

### 规则 1：OpenSpec 文件中不得出现中文字符

**规则**：OpenSpec 规范文件（`openspec/` 目录下的 `.md` 文件）不得包含中文字符。

**验证检查**：
```bash
# 检查 OpenSpec 文件中的中文字符
find openspec/ -name "*.md" -exec grep -l "[\u4e00-\u9fff]" {} \;
```

**预期结果**：不应返回任何文件。

### 规则 2：文件路径验证

**规则**：确保 OpenSpec 文件未被误译或移动。

**有效路径**：
```
✓ openspec/specs/**/spec.md
✓ openspec/changes/{变更名称}/proposal.md
✓ openspec/changes/{变更名称}/tasks.md
✓ openspec/changes/{变更名称}/design.md
```

**无效操作**：
```
✗ 将 OpenSpec 文件移出 openspec/ 目录
✗ 创建中文版本（如 spec-zh.md）
✗ 修改 OpenSpec 文件结构
✗ 重命名 OpenSpec 文件以表示语言
```

### 规则 3：内容验证

**规则**：OpenSpec 文档内容必须仅使用英文。

**有效内容示例**：
```
✓ This is the specification for the weighing module
✓ The API provides CRUD operations for records
✓ Implementation should follow the MVVM pattern
```

**无效内容示例**：
```
✗ 这是称重模块的规范
✗ API 提供记录的 CRUD 操作
✗ 实现应该遵循 MVVM 模式
```

## 验证流程

### 提交前验证

在提交任何变更前，执行以下验证：

```bash
#!/bin/bash

echo "Validating OpenSpec files for Chinese content..."

# Check for Chinese characters in OpenSpec files
CHINESE_FILES=$(find openspec/ -name "*.md" -exec grep -l "[\u4e00-\u9fff]" {} \;)

if [ -n "$CHINESE_FILES" ]; then
    echo "ERROR: Found Chinese characters in OpenSpec files:"
    echo "$CHINESE_FILES"
    exit 1
fi

echo "✓ All OpenSpec files are in English"
exit 0
```

### 自动化验证脚本

在项目根目录创建验证脚本：

```bash
#!/bin/bash
# validate-openspec-english.sh

# Set error exit
set -e

echo "=== OpenSpec English Language Validation ==="
echo ""

# Define OpenSpec file patterns
OPENSPEC_PATTERNS=(
    "openspec/specs/**/*.md"
    "openspec/changes/**/proposal.md"
    "openspec/changes/**/tasks.md"
    "openspec/changes/**/design.md"
    "openspec/changes/**/spec.md"
)

# Check each pattern
for pattern in "${OPENSPEC_PATTERNS[@]}"; do
    echo "Checking: $pattern"

    # Find files matching the pattern
    FILES=$(find openspec/ -name "*.md" | grep -E "(spec|proposal|tasks|design)\.md$" || true)

    for file in $FILES; do
        # Check for Chinese characters
        if grep -q "[\u4e00-\u9fff]" "$file"; then
            echo "  ✗ ERROR: Found Chinese characters in $file"
            echo "    Showing lines with Chinese content:"
            grep -n "[\u4e00-\u9fff]" "$file" | head -5
            exit 1
        else
            echo "  ✓ OK: $file"
        fi
    done
done

echo ""
echo "=== All OpenSpec files validated successfully ==="
exit 0
```

## 与 Git 钩子集成

### 提交前钩子

创建 `.git/hooks/pre-commit` 文件，在提交前校验 OpenSpec 文件语言；若校验失败则中止提交并提示修复。

### 推送前钩子（额外保障）

创建 `.git/hooks/pre-push` 文件，在推送前再次运行验证脚本；若失败则中止推送。

## 人工验证清单

在认为翻译完成前，请确认：

- [ ] `openspec/specs/**/*.md` 中无中文字符
- [ ] `openspec/changes/**/proposal.md` 中无中文字符
- [ ] `openspec/changes/**/tasks.md` 中无中文字符
- [ ] `openspec/changes/**/design.md` 中无中文字符
- [ ] `openspec/changes/**/spec.md` 中无中文字符
- [ ] 未将 OpenSpec 文件移出 `openspec/` 目录
- [ ] 未创建 OpenSpec 文件的中文版本
- [ ] `CLAUDE.md` 保持英文
- [ ] 所有 git 钩子已就位且可执行

## CI/CD 集成

可使用 GitHub Actions 或 Azure Pipelines 在拉取请求或推送时运行 OpenSpec 英文验证（仅检查 openspec 与 CLAUDE.md 变更）。工作流中执行：在 openspec 下查找 .md 并检测中文字符，若发现则报错并退出。

## 文档与培训

## 开发者指南

参与项目的开发者必须：

1. **理解要求**：OpenSpec 文件必须保持英文
2. **运行验证**：在提交变更前始终运行验证
3. **报告问题**：立即报告任何验证失败
4. **遵循流程**：使用既定的翻译工作流
5. **保持更新**：及时更新验证脚本与钩子

### 入职清单

新成员应：阅读本验证规则文档；理解 OpenSpec 英文要求；配置本地 git 钩子；成功运行验证脚本；完成一次无错误的测试提交。

## 故障排除

### 常见问题

**问题**：验证脚本误报  
**解决**：更新字符范围或为特定模式添加例外

**问题**：Git 钩子未执行  
**解决**：确保钩子可执行（`chmod +x .git/hooks/*`）

**问题**：CI/CD 在本地失败而流水线中未失败  
**解决**：检查环境差异（OS、编码等）

### 紧急处理

若验证失败且需紧急处理：定位问题文件 → 审阅触发失败的内容 → 将中文内容移至合适的非 OpenSpec 文件或回滚问题变更 → 重新运行验证 → 仅在验证通过后提交。

## 结论

这些验证规则确保：OpenSpec 规范文档按要求保持英文；OpenSpec 系统持续正常运行；翻译工作不损害系统完整性；全体成员遵循一致实践。定期审阅与更新本规则将维持 OpenSpec 系统有效性，同时支持 MaterialClient 项目的中文本地化目标。
