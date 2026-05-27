import Vditor from 'vditor';
import 'vditor/dist/index.css';

window.MonoNotesEditor = {
    instances: {},

    // 🌟 修复 1 & 2：函数改名为 initVditor，参数顺序严格对齐 C#
    initVditor: function (elementId, initialText, dotNetHelper) {
        const editor = new Vditor(elementId, {
            value: initialText || "",
            height: '100%',
            mode: 'ir',
            icon: 'ant',
            outline: {
                enable: true,
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
            after: () => {
                this.instances[elementId] = editor;
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

                            // 🌟 修复 3：统一使用 dotNetHelper
                            const relativePath = await dotNetHelper.invokeMethodAsync('UploadImageAsync', base64, file.name);

                            // 🌟 修复 4：直接使用当前的 editor 实例插入文本
                            editor.insertValue(`![${file.name}](${relativePath})`);
                        };
                        reader.readAsDataURL(file);
                    });

                    return "图片正在安全保存到本地...";
                }
            }
        });
    },

    // 🌟 修复 1：函数改名为 setVditorContent，匹配 C#
    setVditorContent: function (elementId, text) {
        const editor = this.instances[elementId];
        if (editor) {
            editor.setValue(text || "", true);
        }
    }
};