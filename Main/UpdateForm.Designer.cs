namespace Launcher {
   partial class UpdateForm {
      /// <summary>
      /// Required designer variable.
      /// </summary>
      private System.ComponentModel.IContainer components = null;

      /// <summary>
      /// Clean up any resources being used.
      /// </summary>
      /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
      protected override void Dispose(bool disposing) {
         if (disposing && (components != null)) {
            components.Dispose();
         }
         base.Dispose(disposing);
      }

      #region Windows Form Designer generated code

      /// <summary>
      /// Required method for Designer support - do not modify
      /// the contents of this method with the code editor.
      /// </summary>
      private void InitializeComponent() {
         this.lblMessage = new System.Windows.Forms.Label();
         this.cmdUpdate = new System.Windows.Forms.Button();
         this.cmdClose = new System.Windows.Forms.Button();
         this.SuspendLayout();
         // 
         // lblMessage
         // 
         this.lblMessage.Location = new System.Drawing.Point(13, 9);
         this.lblMessage.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
         this.lblMessage.Name = "lblMessage";
         this.lblMessage.Size = new System.Drawing.Size(492, 46);
         this.lblMessage.TabIndex = 0;
         this.lblMessage.Text = "Đang kiểm tra xem có bản cập nhật nào cho gói hiện hành hay không...";
         // 
         // cmdUpdate
         // 
         this.cmdUpdate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
         this.cmdUpdate.Enabled = false;
         this.cmdUpdate.Location = new System.Drawing.Point(308, 58);
         this.cmdUpdate.Name = "cmdUpdate";
         this.cmdUpdate.Size = new System.Drawing.Size(96, 31);
         this.cmdUpdate.TabIndex = 1;
         this.cmdUpdate.Text = "Cập nhật";
         this.cmdUpdate.UseVisualStyleBackColor = true;
         this.cmdUpdate.Click += new System.EventHandler(this.cmdUpdate_Click);
         // 
         // cmdClose
         // 
         this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
         this.cmdClose.Location = new System.Drawing.Point(410, 58);
         this.cmdClose.Name = "cmdClose";
         this.cmdClose.Size = new System.Drawing.Size(96, 31);
         this.cmdClose.TabIndex = 2;
         this.cmdClose.Text = "Đóng";
         this.cmdClose.UseVisualStyleBackColor = true;
         this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
         // 
         // UpdateForm
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 18F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.ClientSize = new System.Drawing.Size(518, 101);
         this.Controls.Add(this.cmdClose);
         this.Controls.Add(this.cmdUpdate);
         this.Controls.Add(this.lblMessage);
         this.Font = new System.Drawing.Font("Tahoma", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
         this.Margin = new System.Windows.Forms.Padding(4);
         this.Name = "UpdateForm";
         this.Text = "Bảng cập nhật";
         this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.UpdateForm_FormClosing);
         this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.UpdateForm_FormClosed);
         this.Load += new System.EventHandler(this.UpdateForm_Load);
         this.ResumeLayout(false);

      }

      #endregion

      private System.Windows.Forms.Label lblMessage;
      private System.Windows.Forms.Button cmdUpdate;
      private System.Windows.Forms.Button cmdClose;
   }
}