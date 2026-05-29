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

            hint: {
                extend: [
                    {
                        key: '[[',
                        hint: async (value) => {
                            const hints = await dotNetHelper.invokeMethodAsync('SearchNoteHints', value);

                            return hints.map(title => {
                                return {
                                    value: `[🔗 ${title}](note://${encodeURIComponent(title)})`,
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
                    let targetLink = e.target.closest('a');
                    if (targetLink && targetLink.getAttribute('href') && targetLink.getAttribute('href').startsWith('note://')) {
                        e.preventDefault();
                        if (e.ctrlKey || e.metaKey) {
                            const targetTitle = decodeURIComponent(targetLink.getAttribute('href').replace('note://', ''));
                            dotNetHelper.invokeMethodAsync('TriggerInternalLink', targetTitle);
                        }
                        return;
                    }

                    if (!e.ctrlKey && !e.metaKey) return;
                    const selection = window.getSelection();
                    if (!selection || selection.rangeCount === 0) return;
                    let textNode = selection.anchorNode;
                    if (textNode && textNode.nodeType === 3) {
                        const text = textNode.textContent;
                        const offset = selection.anchorOffset;
                        const regex = /\[\[(.*?)\]\]/g;
                        let match;
                        while ((match = regex.exec(text)) !== null) {
                            const start = match.index;
                            const end = match.index + match[0].length;
                            if (offset >= start && offset <= end) {
                                e.preventDefault();
                                dotNetHelper.invokeMethodAsync('TriggerInternalLink', match[1]);
                                break;
                            }
                        }
                    }
                });
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