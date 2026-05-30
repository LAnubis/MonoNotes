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

                {
                    name: 'insert-note',
                    tip: '插入关联笔记',
                    icon: '<svg viewBox="0 0 1024 1024" xmlns="http://www.w3.org/2000/svg" width="22" height="22"><path d="M574 665.4a8.03 8.03 0 0 0-11.3 0L446.5 781.6c-53.8 53.8-144.6 59.5-204 0-59.5-59.5-53.8-150.2 0-204l116.2-116.2c3.1-3.1 3.1-8.2 0-11.3l-39.8-39.8a8.03 8.03 0 0 0-11.3 0L191.4 526.5c-84.6 84.6-84.6 221.5 0 306s221.5 84.6 306 0l116.2-116.2c3.1-3.1 3.1-8.2 0-11.3L574 665.4zm258.6-474c-84.6-84.6-221.5-84.6-306 0L410.3 307.6a8.03 8.03 0 0 0 0 11.3l39.7 39.7c3.1 3.1 8.2 3.1 11.3 0l116.2-116.2c53.8-53.8 144.6-59.5 204 0 59.5 59.5 53.8 150.2 0 204L665.3 562.6a8.03 8.03 0 0 0 0 11.3l39.8 39.8c3.1 3.1 8.2 3.1 11.3 0l116.2-116.2c84.5-84.6 84.5-221.5 0-306.1zM610.1 372.3a8.03 8.03 0 0 0-11.3 0L372.3 598.7a8.03 8.03 0 0 0 0 11.3l39.6 39.6c3.1 3.1 8.2 3.1 11.3 0l226.4-226.4c3.1-3.1 3.1-8.2 0-11.3l-39.5-39.6z"/></svg>',

                    // 🌟 核心替换：使用箭头函数，拦截默认事件，并加上日志
                    click: (event) => {
                        if (event) event.preventDefault();
                        console.log("【前端 JS】插入按钮被点击！准备呼叫 C#...");

                        dotNetHelper.invokeMethodAsync('TriggerInsertNoteModal')
                            .then(() => console.log("【前端 JS】成功呼叫 C# 方法！"))
                            .catch(err => console.error("【前端 JS】呼叫 C# 失败：", err));
                    }
                },
                '|',
                'undo', 'redo', '|',
                'edit-mode',
                'outline'
            ],

            // 注意：这里已经彻底删除了 hint 引擎！

            after: () => {
                window.MonoNotesEditor.instances[elementId] = editor;

                document.getElementById(elementId).addEventListener('click', (e) => {
                    // 保留兼容：按住 Ctrl 点击《《xxx》》跳转的功能
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
                                dotNetHelper.invokeMethodAsync('TriggerInternalLink', match[1].trim());
                                break;
                            }
                        }
                    }
                }, true);
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
        });
    },

    setVditorContent: function (elementId, text) {
        const editor = window.MonoNotesEditor.instances[elementId];
        if (editor) {
            editor.setValue(text || "", true);
        }
    },

    scrollToHeading: function (elementId, headingText) {
        const editor = window.MonoNotesEditor.instances[elementId];
        if (!editor) return;
        const editElement = editor.vditor.ir.element;
        if (!editElement) return;
        const headings = editElement.querySelectorAll('h1, h2, h3, h4, h5, h6');
        for (let i = 0; i < headings.length; i++) {
            let text = headings[i].textContent.replace(/[\u200B-\u200D\uFEFF]/g, '').replace(/^#+\s*/, '').trim();
            if (text === headingText.trim()) {
                const targetOffset = headings[i].offsetTop - 20;
                editElement.scrollTo({ top: targetOffset, behavior: 'smooth' });
                break;
            }
        }
    },

    // 🌟 新增：专门暴露给 C# 调用的插入文本 API
    insertTextAtCursor: function (elementId, text) {
        const editor = window.MonoNotesEditor.instances[elementId];
        if (editor) {
            editor.insertValue(text);
        }
    }
};