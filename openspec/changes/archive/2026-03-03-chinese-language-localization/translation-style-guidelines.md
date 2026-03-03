# Translation Style Guidelines

This document provides comprehensive style guidelines for translating technical documentation and code comments in the MaterialClient project.

## General Principles

### Accuracy First
- Translate the intended meaning, not just the words
- Maintain technical accuracy over literal translation
- Consult technical references when uncertain about terminology
- Ensure code comments accurately describe what the code does

### Consistency
- Use consistent terminology throughout all translations
- Follow the translation glossary for standard terms
- Maintain consistent formatting and structure
- Use the same style for similar types of content

### Clarity
- Write in clear, natural Chinese
- Avoid overly complex sentence structures
- Use simple, direct language when possible
- Ensure translations are easy to understand

## Technical Documentation Guidelines

### Markdown Document Structure
- Preserve all Markdown formatting (headings, lists, code blocks, links, etc.)
- Maintain the original document hierarchy
- Keep code blocks in their original form (English code comments within blocks may be translated)
- Preserve all links and references

### Headings
- Translate heading content to Chinese
- Maintain the same heading level (H1, H2, H3, etc.)
- Keep heading formatting consistent
- Do not translate technical acronyms used as headings (e.g., API, HTTP, JSON)

### Lists
- Translate list items to Chinese
- Maintain list type (ordered, unordered, nested)
- Preserve list indentation
- Keep list markers consistent

### Code Blocks
- Translate comments within code blocks
- Do not translate code itself (variables, methods, classes, etc.)
- Preserve code formatting and indentation
- Keep error messages and log output in their original language

### Links and References
- Translate link text to Chinese
- Preserve all URLs and file paths
- Keep internal reference links functional
- Maintain external link destinations

### Tables
- Translate table content to Chinese
- Preserve table structure and formatting
- Keep table headers consistent
- Maintain column alignment

### Emphasis
- Preserve bold, italic, and other emphasis formatting
- Maintain the same emphasis points
- Do not add or remove emphasis without reason

## Code Comment Guidelines

### Comment Purpose
- Code comments should explain WHY code exists, not WHAT it does
- Describe the purpose and context of the code
- Explain non-obvious logic or algorithms
- Document design decisions and trade-offs

### Comment Style
- Write comments in clear, natural Chinese
- Use concise language
- Keep comments up-to-date with code changes
- Avoid redundant comments that simply restate the code

### Comment Placement
- Place comments before the code they describe
- Keep comments close to the relevant code
- Use inline comments sparingly
- Group related comments together

### Special Comment Types

#### TODO Comments
```csharp
// TODO: 实现新的称重模式处理逻辑
```

#### FIXME Comments
```csharp
// FIXME: 修复车牌识别超时问题
```

#### HACK Comments
```csharp
// HACK: 临时解决方案，等待硬件更新后移除
```

#### NOTE Comments
```csharp
// NOTE: 这个方法需要异步执行以避免阻塞 UI
```

#### WARNING Comments
```csharp
// WARNING: 不要在 UI 线程调用此方法
```

## Technical Terminology Guidelines

### Acronyms and Abbreviations
- Keep common technical acronyms in English: API, HTTP, REST, JSON, XML, etc.
- Use full Chinese translation followed by English in parentheses for less common terms
- Maintain consistency in acronym usage

### Brand and Product Names
- Keep brand names in English: Microsoft, Windows, Avalonia, etc.
- Keep product names in English: Entity Framework, .NET, etc.
- Use Chinese translation when appropriate for the audience

### Code Elements
- Do not translate: variable names, method names, class names, property names, etc.
- Translate comments that describe code elements
- Use Chinese descriptions for what the code does

### File and Path Names
- Keep file names in English (unless they are user-facing)
- Keep directory names in English
- Translate descriptions of file paths
- Maintain full paths in their original form

## Formatting Guidelines

### Punctuation
- Use Chinese punctuation (full-width) for Chinese text
- Use English punctuation for code and technical content
- Be consistent with punctuation usage
- Space between English and Chinese text when appropriate

### Numbers and Dates
- Use Western numerals (0-9)
- Format dates consistently (YYYY-MM-DD recommended)
- Use appropriate number formatting for the audience
- Keep version numbers in standard format

### Whitespace
- Preserve original line breaks
- Maintain paragraph spacing
- Keep indentation consistent
- Use appropriate spacing around punctuation

## Localization Considerations

### Date and Time
- Use 24-hour format for time
- Format dates according to Chinese conventions (年-月-日)
- Consider time zone when displaying timestamps
- Use relative time expressions when appropriate

### Numbers and Units
- Use metric units where appropriate
- Format numbers according to Chinese conventions
- Include units of measurement when relevant
- Be consistent with decimal separators

### Addresses and Contact Information
- Format addresses according to Chinese conventions
- Keep country names in their official form
- Use appropriate formatting for phone numbers
- Maintain postal codes in standard format

## Quality Assurance Guidelines

### Review Process
- Review all translations for accuracy
- Check for consistency with the glossary
- Verify that technical terms are handled correctly
- Ensure formatting is preserved

### Testing
- Test translated documents for readability
- Verify that code comments are accurate
- Check that links and references work
- Ensure UI elements display correctly

### Common Issues to Avoid
- Machine translation that produces unnatural Chinese
- Translating code elements that should remain in English
- Inconsistent terminology usage
- Loss of technical accuracy
- Formatting errors that break document structure

## Tools and Resources

### Recommended Tools
- Professional human translators for complex technical content
- AI translation tools with human review for speed
- Terminology management systems for consistency
- Translation memory tools for reuse

### References
- Microsoft Chinese technical documentation
- Avalonia Chinese documentation
- .NET Chinese documentation
- Industry-standard technical Chinese resources

## Examples

### Good Translation
```csharp
/// <summary>
/// 获取指定车牌号的所有称重记录
/// </summary>
/// <param name="plateNumber">车牌号</param>
/// <returns>称重记录列表</returns>
public async Task<List<WeighingRecord>> GetRecordsByPlateNumber(string plateNumber)
{
    return await _context.WeighingRecords
        .Where(r => r.PlateNumber == plateNumber)
        .ToListAsync();
}
```

### Poor Translation
```csharp
/// <summary>
/// 获取指定车牌号的全部称重记录
/// </summary>
/// <param name="plateNumber">车牌号码</param>
/// <returns>称重记录的列表</returns>
public async Task<List<WeighingRecord>> GetRecordsByPlateNumber(string plateNumber)
{
    return await _context.WeighingRecords
        .Where(r => r.PlateNumber == plateNumber)
        .ToListAsync();
}
```

**Notes:**
- "所有" vs "全部" - both acceptable, be consistent
- "车牌号" vs "车牌号码" - "车牌号" is more concise and commonly used
- "称重记录列表" vs "称重记录的列表" - "称重记录列表" is more concise

## Conclusion

Following these guidelines will ensure that all translations in the MaterialClient project are:
- Accurate and technically correct
- Consistent across all documents and code
- Clear and easy to understand
- Professional and well-formatted

Regular reviews and updates of these guidelines will help maintain translation quality as the project evolves.
