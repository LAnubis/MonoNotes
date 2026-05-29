import Vditor from 'vditor';
import 'vditor/dist/index.css';

window.MonoNotesEditor = {
    instances: {},

    initVditor: function (elementId, initialText, dotNetHelper) {
        const editor = new Vditor(elementId, {
            value: initialText || "",
            height: '100%',
            mode: 'ir',
            icon: 'ant',
            outline: {
                enable: false,
                position: 'right'
            },
            cache: {
                enable: false
            },
            toolbar: [
                'emoji', 'headings', 'bold', 'italic', 'strike', 'link', '|',
                'list', 'ordered-list', 'check', 'outdent', 'indent', '|',
                'quote', 'line', 'code', 'inline-code', 'insert-before', 'insert-after', '|',
                'table', '|',
                'undo', 'redo', '|',
                'edit-mode',
                'outline',
                'export'
            ],

            // 🌟 1. 改为插入 ==标题== 语法
            hint: {
                extend: [
                    {
                        key: '[[',
                        hint: async (value) => {
                            const hints = await dotNetHelper.invokeMethodAsync('SearchNoteHints', value);
                            return hints.map(title => {
                                return {
                                    // 核心：使用 Vditor 高亮语法，生成 mark 标签，彻底抛弃 a 标签
                                    value: `《《${title}》》`,
                                    html: `<div style="display: flex; align-items: center; gap: 8px;">
                                             <span style="color: #2eaadc;">📄</span> 
                                             <span style="font-weight: 500;">${title}</span>
                                           </div>`
                                };
                            });
                        }
                    }
                ]
            },

            after: () => {
                window.MonoNotesEditor.instances[elementId] = editor;

                document.getElementById(elementId).addEventListener('click', (e) => {

                    // 🌟 2. 精准拦截我们生成的 mark 标签
                    let targetMark = e.target.closest('mark');
                    if (targetMark) {
                        // 只有按住 Ctrl (Win) 或 Cmd (Mac) 才跳转
                        if (e.ctrlKey || e.metaKey) {
                            e.preventDefault();
                            e.stopPropagation();
                            // 提取文字内容发送给 C#
                            const title = targetMark.textContent.trim();
                            dotNetHelper.invokeMethodAsync('TriggerInternalLink', title);
                        }
                        return; // 如果没按 Ctrl，光标正常进入文字，允许修改
                    }

                    // 3. 兼容纯文本的 [[xxx]] (以防你手动打字不选联想词)
                    if (!e.ctrlKey && !e.metaKey) return;
                    const selection = window.getSelection();
                    if (!selection || selection.rangeCount === 0) return;
                    let textNode = selection.anchorNode;
                    if (textNode && textNode.nodeType === 3) {
                        const text = textNode.textContent;
                        const offset = selection.anchorOffset;
                        const regex = /《《(.*?)》》/g;
                        let match;
                        while ((match = regex.exec(text)) !== null) {
                            const start = match.index;
                            const end = match.index + match[0].length;
                            if (offset >= start && offset <= end) {
                                e.preventDefault();
                                e.stopPropagation();
                                dotNetHelper.invokeMethodAsync('TriggerInternalLink', match[1]);
                                break;
                            }
                        }
                    }
                }, true); // 🌟 必须为 true
            },
            input: (value) => {
                dotNetHelper.invokeMethodAsync('UpdateContent', value);
            },

            upload: {
                accept: 'image/*, .jpg, .png, .gif, .svg, .webp',
                handler(files) {
                    Array.from(files).forEach(file => {
                        const reader = new FileReader();
                        reader.onload = async (e) => {
                            const base64 = e.target.result;
                            const relativePath = await dotNetHelper.invokeMethodAsync('UploadImageAsync', base64, file.name);
                            editor.insertValue(`![${file.name}](${relativePath})`);
                        };
                        reader.readAsDataURL(file);
                    });
                    return "图片正在安全保存到本地...";
                }
            }
        }); // ⬅️ Vditor 的初始化在这里结束了！
    }, // ⬅️ initVditor 方法在这里结束了！

    setVditorContent: function (elementId, text) {
        const editor = window.MonoNotesEditor.instances[elementId];
        if (editor) {
            editor.setValue(text || "", true);
        }
    }, // ⬅️ 必须有这个逗号

    // 🌟 修复：scrollToHeading 被移到了最外层，成为了 MonoNotesEditor 的合法成员！
    scrollToHeading: function (elementId, headingText) {
        const editor = window.MonoNotesEditor.instances[elementId];
        if (!editor) return;

        const editElement = editor.vditor.ir.element;
        if (!editElement) return;

        const headings = editElement.querySelectorAll('h1, h2, h3, h4, h5, h6');
        for (let i = 0; i < headings.length; i++) {
            let text = headings[i].textContent
                .replace(/[\u200B-\u200D\uFEFF]/g, '')
                .replace(/^#+\s*/, '')
                .trim();

            if (text === headingText.trim()) {
                const targetOffset = headings[i].offsetTop - 20;
                editElement.scrollTo({
                    top: targetOffset,
                    behavior: 'smooth'
                });
                break;
            }
        }
    }
};