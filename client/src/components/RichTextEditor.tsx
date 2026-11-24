import React, { Suspense } from 'react';

const TinyMceEditor = React.lazy(async () => ({ default: (await import('@tinymce/tinymce-react')).Editor }));

export function RichTextEditor({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <div className="richTextEditor">
      <Suspense fallback={<div className="richTextLoading">Načítám editor…</div>}>
        <TinyMceEditor
          tinymceScriptSrc="/tinymce/tinymce.min.js"
          licenseKey="gpl"
          value={value}
          onEditorChange={(content: string) => onChange(content)}
          init={{
            height: 430,
            menubar: false,
            branding: false,
            promotion: false,
            plugins: 'autolink code link lists preview quickbars table wordcount',
            toolbar: 'undo redo | blocks | bold italic underline | bullist numlist | link table | removeformat | preview code',
            block_formats: 'Odstavec=p; Nadpis H1=h1; Nadpis H2=h2; Nadpis H3=h3',
            quickbars_selection_toolbar: 'bold italic underline | blocks | link',
            contextmenu: 'link table',
            valid_elements: 'h1,h2,h3,h4,p,br,strong/b,em/i,u,ul,ol,li,a[href|target|rel],table,thead,tbody,tr,th,td,blockquote',
            link_default_target: '_blank',
            rel_list: [
              { title: 'Bez rel', value: '' },
              { title: 'No opener', value: 'noopener noreferrer' },
            ],
            content_style: 'body{font-family:Inter,system-ui,sans-serif;color:#10231D;line-height:1.65} h1,h2,h3{color:#071f1a;line-height:1.12} table{width:100%;border-collapse:collapse} th,td{border:1px solid #D7E7DC;padding:8px}',
          }}
        />
      </Suspense>
      <div className="richTextHint">Editor ukládá HTML se skutečnými tagy pro nadpisy, seznamy, odkazy a tabulky.</div>
    </div>
  );
}
