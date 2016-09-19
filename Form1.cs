using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace COREM2
{
    public partial class frmMain : Form
    {
//VARIABLES
        public Dictionary<Type, SqlDbType> typeMap;
        public List<string> alltables = new List<string>();
        public List<string> allconstraints = new List<string>();
        //Constraint format TYPE_ChildField_ChildTable_ParentField_ParentTable
        public List<SqlDataAdapter> adapter = new List<SqlDataAdapter>();
        public List<BindingSource> binding = new List<BindingSource>();
        public DataSet ds;
        public CurrencyManager cm;
//FORM
        public frmMain()
        {
            InitializeComponent();
        }
        private void frmMain_Load(object sender, EventArgs e)
        {
            pnlSecurity.Dock = DockStyle.Fill;
        }
        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveData();
        }
        //DATA
        private int tablindex(string table)
        {
            return alltables.IndexOf(table);
        }
        private SqlDataAdapter adp (string table)
        {
            return adapter[tablindex(table)];
        }
        private DataTable tabl(string table="", int? index = null)
        {
            if (!(table==""))
            {
                return ds.Tables[table];
            }
            else
            {
                return ds.Tables[(int)index];
            }
            
        }
        private void LoadData(string table)
        {
            tabl(table).Clear();
            adp(table).Fill(tabl(table));
        }
        private void SaveData(string table = "")
        {
            if (table=="")
            {
                int i = 0;
                foreach (var ad in adapter)
                {
                    var changes = tabl(index: i).GetChanges();
                    tabl(index: i).AcceptChanges();
                    if (changes != null)
                    {
                        try
                        {
                            ad.Update(changes);
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("DELETE"))
                            {
                                MessageBox.Show("You cannot delete the current record as it is connected to another record held in the database");
                            }
                            else if (ex.Message.Contains("INSERT"))
                            {

                            }
                            else if (ex.Message.Contains("DELETE"))
                            {

                            }
                        }
                    }
                    i++;
                }
            }
            else
            {
                var changes = tabl(table:table).GetChanges();
                tabl(table: table).AcceptChanges();
                if (changes != null)
                {
                    try
                    {
                        adapter[tablindex(table)].Update(changes);
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("DELETE"))
                        {
                            MessageBox.Show("You cannot delete the current record as it is connected to another record held in the database");
                        }
                        else if (ex.Message.Contains("INSERT"))
                        {

                        }
                        else if (ex.Message.Contains("DELETE"))
                        {

                        }
                    }
                }
            }
            
        }
        public void InitialiseCatalogue()
        {
            //Read catalogue name and password from user input
            string cs1 = txtCs1.Text;
            string cs2 = txtCs2.Text;
            string cs3 = txtCs3.Text;

            //Create sql connection string and open catalogue
            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();
            SqlConnection sc = new SqlConnection();
            csb.InitialCatalog = cs1;
            csb.UserID = cs2;
            csb.Password = cs3;
            csb.DataSource = "dns-netstore.dlinkddns.com,50911";
            sc.ConnectionString = csb.ConnectionString;

            //Open the connection in a new thread
            Task.Factory.StartNew(() =>
            {
                try
                {
                    SetPrg(message: "Opening catalog...", marquee: true);
                    SetView(null, new Control[] {txtCs1,txtCs2,txtCs3,btnLogin });
                    try
                    {
                        pnlSecurity.Invoke((MethodInvoker)delegate
                        {
                            pnlSecurity.Visible = false;
                            pnlBody.Visible = true;
                            lblPrg.BackColor = Color.Honeydew;
                        });
                        //Get schema and UQ, FK and PK lists
                        LoadSchema(sc);
                        //Create data adapters required
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Unable to open the catalogue. Check your credentials and network connection and try again.");
                    }                   
                    SetView(new Control[] { txtCs1, txtCs2, txtCs3, btnLogin },null);
                    SetPrg(type: "reset");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Something went wrong.");
                }
            });
        }
        private void LoadSchema(SqlConnection sc)
        {
            //Get schema from database
            var schemaresult = GetSchema(sc);
            if (schemaresult != null)
            {
                alltables = schemaresult[0] as List<string>;
                allconstraints = schemaresult[1] as List<string>;
            }

            //Create table adapters from schema
            if (alltables != null && alltables.Count > 0)
            {
                adapter = new List<SqlDataAdapter>();
                ds = new DataSet();
                SetPrg(message: "", max: alltables.Count, min: 0);
                for (int i = 0; i < alltables.Count; i++)
                {
                    SetPrg(message: "Building table adapter for " + alltables[i], inc: 1);
                    var result = SetAdapter(alltables[i],sc);
                    adapter.Add(result[0] as SqlDataAdapter);
                    ds = result[1] as DataSet;
                    binding.Add(SetBinding(alltables[i]));
                }
                SetPrg("reset");
            }

            //Set unique constraints on tables
            SetPrg(message: "", max: alltables.Count, min: 0);
            foreach (var table in alltables)
            {
                SetPrg(message: "Setting unique constraints for " + table, inc: 1);
                foreach (var constraint in allconstraints)
                {
                    string[] split = constraint.Split('_');
                    if (split[2] == table)
                    {
                        foreach (DataColumn col in tabl(table).Columns)
                        {
                            if (col.ColumnName == split[1] && split[0] == "PK")
                            {
                                col.Unique = true;
                            }
                            if (col.ColumnName == split[1])
                            {
                                col.Caption = constraint;
                            }
                        }
                    }
                }
            }
            SetPrg("reset");
        }
        private object[] GetSchema(SqlConnection sc)
        {
            try
            {
                List<string> tables = new List<string>();
                List<string> constraints = new List<string>();

                SqlCommand tablescmd = new SqlCommand();
                tablescmd.CommandText = "SELECT name FROM sys.tables";
                tablescmd.Connection = sc;
                tablescmd.CommandType = CommandType.Text;
                sc.Open();
                var reader = tablescmd.ExecuteReader();
                if (reader.HasRows)
                {
                    while (reader.Read())
                    {
                        try
                        {
                            //get table name
                            tables.Add(reader.GetString(0));
                        }
                        catch { }
                    }
                }
                sc.Close();

                SqlCommand constraintcmd = new SqlCommand();
                constraintcmd.CommandText = "SELECT CONSTRAINT_NAME from INFORMATION_SCHEMA.CONSTRAINT_COLUMN_USAGE";
                constraintcmd.Connection = sc;
                constraintcmd.CommandType = CommandType.Text;
                sc.Open();
                var uniqreader = constraintcmd.ExecuteReader();
                if (uniqreader.HasRows)
                {
                    while (uniqreader.Read())
                    {
                        try
                        {
                            //Get constraint name
                            constraints.Add(uniqreader.GetString(0));
                        }
                        catch { }
                    }
                }
                sc.Close();

                object[] result = new object[2] { tables, constraints };
                return result;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        private object[] SetAdapter(string table, SqlConnection sc)
        {
            InitTypeMap();
            SqlDataAdapter newadapter = new SqlDataAdapter();
            string start = "";
            string end = "";
            string join = "";
            bool IDcol = true;
            //SELECT
            newadapter.SelectCommand = new SqlCommand();
            newadapter.SelectCommand.Connection = sc;
            newadapter.SelectCommand.CommandText = "SELECT * FROM " + table;
            newadapter.SelectCommand.CommandType = CommandType.Text;
            newadapter.FillSchema(ds, SchemaType.Source, table);
            int j = ds.Tables.IndexOf(table);
            int totcol = ds.Tables[j].Columns.Count - 1;
            //INSERT
            start = "";
            end = ")";
            join = ", ";
            newadapter.InsertCommand = new SqlCommand();
            newadapter.InsertCommand.Connection = sc;
            newadapter.InsertCommand.CommandText = "INSERT INTO " + table + " (";
            IDcol = true;
            foreach (DataColumn col in ds.Tables[j].Columns)
            {
                if (!IDcol)
                {
                    newadapter.InsertCommand.CommandText += col.ColumnName;
                    if (ds.Tables[j].Columns.IndexOf(col) == totcol)
                    {
                        newadapter.InsertCommand.CommandText += end;
                    }
                    else
                    {
                        newadapter.InsertCommand.CommandText += join;
                    }
                }
                else
                {
                    IDcol = false;
                }
            }
            newadapter.InsertCommand.CommandText += " VALUES(";
            end = ");";
            IDcol = true;
            foreach (DataColumn col in ds.Tables[j].Columns)
            {
                if (!IDcol)
                {
                    newadapter.InsertCommand.CommandText += "@" + col.ColumnName;
                    if (ds.Tables[j].Columns.IndexOf(col) == totcol)
                    {
                        newadapter.InsertCommand.CommandText += end;
                    }
                    else
                    {
                        newadapter.InsertCommand.CommandText += join;
                    }
                }
                else
                {
                    IDcol = false;
                }
            }
            foreach (DataColumn col in ds.Tables[j].Columns)
            {
                var systype = col.DataType;
                var sqltype = typeMap[systype];
                newadapter.InsertCommand.Parameters.Add(new SqlParameter("@" + col.ColumnName, sqltype, col.MaxLength, col.ColumnName));
            }
            //UPDATE
            newadapter.UpdateCommand = new SqlCommand();
            newadapter.UpdateCommand.Connection = sc;
            newadapter.UpdateCommand.CommandText = "UPDATE " + table + " SET ";
            start = "(";
            end = " WHERE (Serial = @Serial);";
            join = ", ";
            foreach (DataColumn col in ds.Tables[j].Columns)
            {
                if (!col.AutoIncrement)
                {
                    newadapter.UpdateCommand.CommandText += "[" + col.ColumnName + "] = @" + col.ColumnName;

                    if (ds.Tables[j].Columns.IndexOf(col) == totcol)
                    {
                        newadapter.UpdateCommand.CommandText += end;
                    }
                    else
                    {
                        newadapter.UpdateCommand.CommandText += join;
                    }
                }
                else
                {
                    if (ds.Tables[j].Columns.IndexOf(col) == totcol)
                    {
                        newadapter.UpdateCommand.CommandText = newadapter.UpdateCommand.CommandText.TrimEnd(' ').TrimEnd(',') + " " + end;
                    }
                }
            }
            newadapter.UpdateCommand.CommandType = CommandType.Text;
            foreach (DataColumn col in ds.Tables[j].Columns)
            {
                var systype = col.DataType;
                var sqltype = typeMap[systype];
                newadapter.UpdateCommand.Parameters.Add(new SqlParameter("@" + col.ColumnName, sqltype, col.MaxLength, col.ColumnName));
                newadapter.UpdateCommand.Parameters.Add(new SqlParameter("@Original_" + col.ColumnName, sqltype, col.MaxLength, col.ColumnName));
            }
            //DELETE
            start = "";
            end = ")";
            join = ", ";
            newadapter.DeleteCommand = new SqlCommand();
            newadapter.DeleteCommand.Connection = sc;
            newadapter.DeleteCommand.CommandText = "DELETE FROM " + table + " WHERE (Serial = @Serial)";
            newadapter.DeleteCommand.CommandType = CommandType.Text;
            newadapter.DeleteCommand.Parameters.Add(new SqlParameter("@Serial", SqlDbType.Int, ds.Tables[j].Columns[0].MaxLength, "Serial"));

            return new object[] { newadapter, ds };
        }
        private BindingSource SetBinding(string table)
        {
            BindingSource newbinding = new BindingSource();
            newbinding.DataSource = tabl(table);
            return newbinding;
        }
        private void InitTypeMap()
        {
            typeMap = new Dictionary<Type, SqlDbType>();
            typeMap[typeof(string)] = SqlDbType.VarChar;
            typeMap[typeof(char[])] = SqlDbType.NVarChar;
            typeMap[typeof(int)] = SqlDbType.Int;
            typeMap[typeof(Int32)] = SqlDbType.Int;
            typeMap[typeof(Int16)] = SqlDbType.SmallInt;
            typeMap[typeof(Int64)] = SqlDbType.BigInt;
            typeMap[typeof(Byte[])] = SqlDbType.VarBinary;
            typeMap[typeof(Boolean)] = SqlDbType.Bit;
            typeMap[typeof(DateTime)] = SqlDbType.DateTime2;
            typeMap[typeof(DateTimeOffset)] = SqlDbType.DateTimeOffset;
            typeMap[typeof(Decimal)] = SqlDbType.Decimal;
            typeMap[typeof(Double)] = SqlDbType.Float;
            typeMap[typeof(Decimal)] = SqlDbType.Money;
            typeMap[typeof(Byte)] = SqlDbType.TinyInt;
            typeMap[typeof(TimeSpan)] = SqlDbType.Time;
        }
        private string CreateCalcField(string typ1, string typ2, string typ3, string typ4, string val1, string val2, string val3, string val4, DataRow row)
        {
            List<string> types = new List<string>() { typ1, typ2, typ3, typ4 };
            List<string> values = new List<string>() { val1, val2, val3, val4 };
            var TypVal = types.Zip(values, (t, v) => new { type = t, value = v });

            string calced = "";
            Random rnd = new Random();
            int i = 2;
            foreach (var tv in TypVal)
            {
                int no = 0;
                switch (tv.type)
                {
                    case "None":
                        break;
                    case "Date (yyyymmdd)":
                        calced += DateTime.Now.ToString("yyyyMMdd");
                        break;
                    case "Date (yyyymmddhhmmss)":
                        calced += DateTime.Now.ToString("yyyyMMddHHmmss");
                        break;
                    case "Random number (2)":
                        no = rnd.Next(int.Parse(tv.value), 99);
                        calced += no.ToString().PadLeft(2, '0');
                        break;
                    case "Random number (3)":
                        no = rnd.Next(int.Parse(tv.value), 999);
                        calced += no.ToString().PadLeft(3, '0');
                        break;
                    case "Random number (4)":
                        no = rnd.Next(int.Parse(tv.value), 9999);
                        calced += no.ToString().PadLeft(4, '0');
                        break;
                    case "Random number (5)":
                        no = rnd.Next(int.Parse(tv.value), 99999);
                        calced += no.ToString().PadLeft(5, '0');
                        break;
                    case "Sequential number (2)":
                        if (int.Parse(tv.value)>=99)
                        {
                            no = 1;
                            row[i] = "1";
                            SaveData("CalculatedFields");
                        }
                        else
                        {
                            no = int.Parse(tv.value) + 1;
                            row[i] = int.Parse(tv.value) + 1;
                            SaveData("CalculatedFields");
                        }
                        calced += no.ToString().PadLeft(2, '0');
                        break;
                    case "Sequential number (3)":
                        if (int.Parse(tv.value) >= 999)
                        {
                            no = 1;
                            row[i] = "1";
                            SaveData("CalculatedFields");
                        }
                        else
                        {
                            no = int.Parse(tv.value) + 1;
                            row[i] = int.Parse(tv.value) + 1;
                            SaveData("CalculatedFields");
                        }
                        calced += no.ToString().PadLeft(3, '0');
                        break;
                    case "Sequential number (4)":
                        if (int.Parse(tv.value) >= 9999)
                        {
                            no = 1;
                            row[i] = "1";
                            SaveData("CalculatedFields");
                        }
                        else
                        {
                            no = int.Parse(tv.value) + 1;
                            row[i] = int.Parse(tv.value) + 1;
                            SaveData("CalculatedFields");
                        }
                        calced += no.ToString().PadLeft(4, '0');
                        break;
                    case "Sequential number (5)":
                        if (int.Parse(tv.value) >= 99999)
                        {
                            no = 1;
                            row[i] = "1";
                            SaveData("CalculatedFields");
                        }
                        else
                        {
                            no = int.Parse(tv.value) + 1;
                            row[i] = int.Parse(tv.value) + 1;
                            SaveData("CalculatedFields");
                        }
                        calced += no.ToString().PadLeft(5, '0');
                        break;
                    case "Random letter (2)":
                        break;
                    case "Random letter (3)":
                        break;
                    case "Random letter (4)":
                        break;
                    case "Random letter (5)":
                        break;
                    case "Sequential letter (2)":
                        break;
                    case "Sequential letter (3)":
                        break;
                    case "Sequential letter (4)":
                        break;
                    case "Sequential letter (5)":
                        break;
                }
                i += 2;
            }
            
            return calced;
        }
//DISPLAY
        private void SetPrg(string type = "run", string message = "", int? inc = null, int? max = null, 
            int? min = null, bool marquee=false)
        {
            if (type == "reset")
            {
                if (barProg.InvokeRequired)
                {
                    barProg.Invoke((MethodInvoker)delegate
                    {
                        barProg.Style = ProgressBarStyle.Continuous;
                        barProg.Maximum = 0;
                        barProg.Minimum = 0;
                        lblPrg.Text = "";
                    });
                }
                else
                {
                    barProg.Style = ProgressBarStyle.Continuous;
                    barProg.Maximum = 0;
                    barProg.Minimum = 0;
                    lblPrg.Text = "";
                }
            }
            else if (marquee)
            {
                if (barProg.InvokeRequired)
                {
                    barProg.Invoke((MethodInvoker)delegate
                    {
                        barProg.Style = ProgressBarStyle.Marquee;
                        barProg.MarqueeAnimationSpeed = 30;
                        lblPrg.Text = message;
                    });
                }
                else
                {
                    barProg.Style = ProgressBarStyle.Marquee;
                    barProg.MarqueeAnimationSpeed = 30;
                    lblPrg.Text = message;
                }
            }
            else
            {
                if (barProg.InvokeRequired)
                {
                    barProg.Invoke((MethodInvoker)delegate
                    {
                        barProg.Style = ProgressBarStyle.Continuous;
                        if (max != null) { barProg.Maximum = (int)max; }
                        if (min != null) { barProg.Minimum = (int)min; }
                        if (inc != null) { barProg.Increment((int)inc); }
                        lblPrg.Text = message;
                    });
                }
                else
                {
                    barProg.Style = ProgressBarStyle.Continuous;
                    if (max != null) { barProg.Maximum = (int)max; }
                    if (min != null) { barProg.Minimum = (int)min; }
                    if (inc != null) { barProg.Increment((int)inc); }
                    lblPrg.Text = message;
                }
            }
        }
        private void SetView(Control[] enabled, Control[] disabled)
        {
            if (enabled != null)
            {
                foreach (var ctrl in enabled)
                {
                    if (ctrl.InvokeRequired)
                    {
                        ctrl.Invoke((MethodInvoker)delegate
                        {
                            ctrl.Enabled = true;
                        });
                    }
                    else
                    {
                        ctrl.Enabled = true;
                    }
                }
            }
            if (disabled != null)
            {
                foreach (var ctrl in disabled)
                {
                    if (ctrl.InvokeRequired)
                    {
                        ctrl.Invoke((MethodInvoker)delegate
                        {
                            ctrl.Enabled = false;
                        });
                    }
                    else
                    {
                        ctrl.Enabled = false;
                    }
                }
            }
        }
        private void OpenTab(TabPage page, string pagename)
        {
            if (page!=null)
            {
                tabBody.TabPages.Add(page);
                tabBody.SelectedTab = page;
            }
            else
            {
                tabBody.SelectedTab = tabBody.TabPages[pagename];
            }
        }
        private void CloseTab(TabPage page)
        {
            tabTemplate.TabPages.Add(page);
        }
        private DataGridView SetGrid(string table)
        {
            DataGridView grid = CtrlGrid("dgv" + table,datasrc:tabl(table));
            tabBody.TabPages[table].Controls.RemoveByKey("dgv" + table);
            tabBody.TabPages[table].Controls.Add(grid);
            grid.BringToFront();
            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.ReadOnly = true;
                col.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                if (col.Name=="Serial")
                {
                    col.Visible = false;
                }
            }
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToAddRows = false;
            return grid;
        }
        private void CreateCardTab(TabPage page, string table = "", int xloc = 10, int yloc = 35, bool enabled = true, int posn=0, bool newrec = false)
        {
            if (newrec)
            {
                binding[tablindex(table)].AddNew();
                LoadData("CalculatedFields");
            }
            else
            {
                binding[tablindex(table)].Position = posn;
            }
            string typ1 = "";
            string typ2 = "";
            string typ3 = "";
            string typ4 = "";
            string val1 = "";
            string val2 = "";
            string val3 = "";
            string val4 = "";
            foreach (DataColumn col in tabl(table).Columns)
            {
                DataRow persistrow = null;
                foreach (DataRow row in tabl("CalculatedFields").Rows)
                {
                    if (col.ColumnName==row["Fieldname"].ToString() && table == row["Tablename"].ToString())
                    {
                        typ1 = row["Type1"].ToString();
                        typ2 = row["Type2"].ToString();
                        typ3 = row["Type3"].ToString();
                        typ4 = row["Type4"].ToString();
                        val1 = row["Val1"].ToString();
                        val2 = row["Val2"].ToString();
                        val3 = row["Val3"].ToString();
                        val4 = row["Val4"].ToString();
                        persistrow = row;
                    }
                    else
                    {
                        typ1 = "";
                        typ2 = "";
                        typ3 = "";
                        typ4 = "";
                        val1 = "";
                        val2 = "";
                        val3 = "";
                        val4 = "";
                    }
                }
                int hbox = 25;
                int wbox = 200;
                int space = 35;
                //Check for constraint
                if (col.Caption.Substring(0,2)=="FK")
                {
                    var split = col.Caption.Split('_');
                    string fktable = split[4];
                    string fkfield = split[3];
                    string childfield = split[1];
                    string displaymember = split[5];
                    LoadData(fktable);
                    int fkposn = 0;
                    try
                    {
                        fkposn = tabl(fktable).Rows.IndexOf(tabl(fktable).AsEnumerable().Where(
                                        r => int.Parse(r["Serial"].ToString()) == int.Parse(tabl(table).Rows[posn][childfield].ToString())
                                        ).ToList().First());
                    }
                    catch (Exception ex)
                    {
                    }
                    Label newlbl = CtrlLabel("lbl" + col.ColumnName, col.ColumnName, autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                    yloc += space;
                    ComboBox newcmb = CtrlCombo("cmb" + col.ColumnName,autosize:false,width:wbox, height: hbox, xloc: xloc, yloc: yloc, enabled: enabled, 
                        datasrc: binding[alltables.IndexOf(fktable)], dsp: displaymember, val: fkfield, selected:0, onclick: Addrecord_FKchange);
                    TextBox newtxt = CtrlText("txt" + col.ColumnName, autosize: false, width: 0, height: 0, xloc: 430, yloc: 35, 
                        datasrc: binding[alltables.IndexOf(table)], dsp: col.ColumnName, val: "Text", visible: true);
                    yloc += space;
                    page.Controls.Add(newlbl);
                    newlbl.BringToFront();
                    page.Controls.Add(newtxt);
                    newtxt.BringToFront();
                    page.Controls.Add(newcmb);
                    newcmb.BringToFront();
                    newcmb.SelectedIndex = 0;
                    CreateCardTab(page, fktable, xloc + 210, 35,false, fkposn, false);
                }
                else
                {
                    if (col.ColumnName!="Serial")
                    {
                        Label newlbl = CtrlLabel("lbl" + col.ColumnName, col.ColumnName, autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                        yloc += space;
                        if (typ1==""||typ1=="None")
                        {
                            TextBox newtxt = CtrlText("txt" + col.ColumnName, autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc, enabled: enabled, datasrc: binding[alltables.IndexOf(table)], dsp: col.ColumnName
                            , val: "Text");
                            page.Controls.Add(newlbl);
                            newlbl.BringToFront();
                            page.Controls.Add(newtxt);
                            newtxt.BringToFront();
                        }
                        else
                        {
                            TextBox newtxt = CtrlText("txt" + col.ColumnName, autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc, enabled: false, datasrc: binding[alltables.IndexOf(table)], dsp: col.ColumnName
                            , val: "Text");
                            page.Controls.Add(newlbl);
                            newlbl.BringToFront();
                            page.Controls.Add(newtxt);
                            newtxt.BringToFront();
                            newtxt.Text  = CreateCalcField(typ1, typ2, typ3, typ4, val1, val2, val3, val4, persistrow);
                            
                        }
                        yloc += space;
                    }
                }
            }
        }
        private void CreateCalcTab(TabPage page, int xloc = 0, int yloc = 35)
        {
            LoadData("CalculatedFields");
            txtXLoc.Text = xloc.ToString();
            int hbox = 25;
            int wbox = 200;
            int yspace = 35;
            int xspace = 220;
            int count = 0;
            foreach (var table in alltables)
            {
                if (table!="CalculatedFields")
                {
                    foreach (DataColumn col in tabl(table).Columns)
                    {
                        foreach (DataRow row in tabl("CalculatedFields").Rows)
                        {
                            if (row["Fieldname"].ToString() == col.ColumnName && row["Tablename"].ToString() == table)
                            {
                                Label newlbl = CtrlLabel("name" + count.ToString().PadLeft(2, '0'), col.ColumnName + " (" + table + ")", autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                                pnlCalculatedFields_Body.Controls.Add(newlbl);
                                Button newbtn = CtrlButton("remv" + count.ToString().PadLeft(2, '0'), "Remove", autosize: false, width: wbox, height: hbox, xloc: xloc + xspace, yloc: yloc,click:btnremovecalcfield);
                                pnlCalculatedFields_Body.Controls.Add(newbtn);
                                newlbl.BringToFront();
                                yloc += yspace;
                                for (int i = 1; i <= 4; i++)
                                {
                                    ComboBox newcmb = CtrlCombo("type" + count.ToString().PadLeft(2, '0') + i.ToString().PadLeft(2, '0'),
                                        new List<string> {"None", "Date (yyyymmdd)", "Date (yyyymmddhhmmss)", "Random number (2)", "Random number (3)",
                                        "Random number (4)", "Random number (5)", "Sequential number (2)", "Sequential number (3)",
                                        "Sequential number (4)", "Sequential number (5)", "Random letter (2)", "Random letter (3)",
                                        "Random letter (4)", "Random letter (5)", "Sequential letter (2)", "Sequential letter (3)",
                                        "Sequential letter (4)", "Sequential letter (5)" }
                                        , false, hbox, wbox, xloc, yloc);
                                    yloc += yspace;
                                    TextBox newtxt = CtrlText("valu" + count.ToString().PadLeft(2, '0') + i.ToString().PadLeft(2, '0'), autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                                    xloc += xspace;
                                    yloc -= yspace;
                                    pnlCalculatedFields_Body.Controls.Add(newcmb);
                                    newtxt.BringToFront();
                                    pnlCalculatedFields_Body.Controls.Add(newtxt);
                                    newtxt.BringToFront();
                                    newcmb.Text = row["Type" + i].ToString();
                                    newtxt.Text = row["Val" + i].ToString();
                                }
                                yloc += (yspace*2);
                                xloc = int.Parse(txtXLoc.Text);
                                count++;
                            }
                        }
                    } 
                }
            }
            txtYLoc.Text = yloc.ToString();
            txtXSpace.Text = xspace.ToString();
            txtYSpace.Text = yspace.ToString();
            txtWBox.Text = wbox.ToString();
            txtHBox.Text = hbox.ToString();
            txtCount.Text = count.ToString();
        }
        private void ClearControls(TabPage page =null, Panel pnl = null, string filter = "")
        {
            if (page!=null)
            {
                var ctrllist = page.Controls;
                for (int i = ctrllist.Count - 1; i >= 0; i--)
                {
                    if (ctrllist[i].Tag == null || ctrllist[i].Tag.ToString() == filter)
                    {
                        page.Controls.Remove(ctrllist[i]);
                    }
                }
            }
            else
            {
                var ctrllist = pnl.Controls;
                for (int i = ctrllist.Count - 1; i >= 0; i--)
                {
                    if (ctrllist[i].Tag == null || ctrllist[i].Tag.ToString() == filter)
                    {
                        pnl.Controls.Remove(ctrllist[i]);
                    }
                }
            }
        }
        private List<Control> SearchControls(Panel pnl = null, TabPage page = null, string filter = "")
        {
            List<Control> ctrllist = new List<Control>();
            if (pnl==null)
            {
                Control[] ctrlarray = new Control[page.Controls.Count];
                page.Controls.CopyTo(ctrlarray, 0);
                ctrllist = ctrlarray.Where(r => r.Name.Contains(filter)).ToList();
            }
            else
            {
                Control[] ctrlarray = new Control[pnl.Controls.Count];
                pnl.Controls.CopyTo(ctrlarray, 0);
                ctrllist = ctrlarray.Where(r => r.Name.Contains(filter)).ToList();
            }
            return ctrllist;
        }
        private void RefreshCalcFields()
        {
            ClearControls(pnl: pnlCalculatedFields_Body);
            CreateCalcTab(tabBody.TabPages["CalculatedFields"]);
        }
//BUTTON CLICKS
        private void btnLogin_Click(object sender, EventArgs e)
        {
            InitialiseCatalogue();
        }
        private void btntableopen_Click(object sender, EventArgs e)
        {
            LoadData(((Button)sender).Text);
            OpenTab(tabTemplate.TabPages[((Button)sender).Text], ((Button)sender).Text);
            
            DataGridView grid = SetGrid(((Button)sender).Text);
            grid.Update();
        }
        private void btntableclose_Click(object sender, EventArgs e)
        {
            try
            {
                adapter[alltables.FindIndex(r => r == ((Button)sender).Tag.ToString())].Update(ds.Tables[((Button)sender).Tag.ToString()]);
            }
            catch
            {
                MessageBox.Show("Update failed.");
            }
            CloseTab(tabBody.TabPages[((Button)sender).Tag.ToString()]);
        }
        private void btncloseviewtab_Click(object sender, EventArgs e)
        {
            CloseTab(tabBody.TabPages["View" + ((Button)sender).Tag.ToString()]);
        }
        //record operations*****
        private void btnaddrecord_Click(object sender, EventArgs e)
        {
            foreach (Control ctrl in tabTemplate.TabPages["Add" + ((Button)sender).Tag.ToString()].Controls)
            {
                try
                {
                    if (ctrl.Tag.ToString() == "")
                    {
                        tabTemplate.TabPages["Add" + ((Button)sender).Tag.ToString()].Controls.Remove(ctrl);
                    }
                }
                catch (Exception ex)
                {

                }
                
            }
            OpenTab(tabTemplate.TabPages["Add" + ((Button)sender).Tag.ToString()], "Add" + ((Button)sender).Tag.ToString());
            CreateCardTab(tabBody.TabPages["Add" + ((Button)sender).Tag.ToString()], ((Button)sender).Tag.ToString(),newrec:true);
        }
        private void btnviewrecord_Click(object sender, EventArgs e)
        {
            DataGridView grid = tabBody.TabPages[((Button)sender).Tag.ToString()].Controls["dgv" + ((Button)sender).Tag.ToString()] as DataGridView;
            try
            {
                int posn = tabl(((Button)sender).Tag.ToString()).Rows.IndexOf(tabl(((Button)sender).Tag.ToString()).AsEnumerable().Where(
                        r => int.Parse(r["Serial"].ToString()) == int.Parse(grid.SelectedRows[0].Cells["Serial"].Value.ToString())).ToList().First());
                OpenTab(tabTemplate.TabPages["View" + ((Button)sender).Tag.ToString()], "View" + ((Button)sender).Tag.ToString());
                CreateCardTab(tabBody.TabPages["View" + ((Button)sender).Tag.ToString()], ((Button)sender).Tag.ToString(), posn: posn, enabled: false);
            }
            catch (Exception ex)
            {

            }
        }
        private void btnsaverecord_Click(object sender, EventArgs e)
        {
            try
            {
                binding[tablindex(((Button)sender).Tag.ToString())].EndEdit();
                SaveData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed.");
            }
            CloseTab(tabBody.TabPages["Add" + ((Button)sender).Tag.ToString()]);
            OpenTab(tabTemplate.TabPages[((Button)sender).Tag.ToString()], ((Button)sender).Tag.ToString());
            DataGridView grid = SetGrid(((Button)sender).Tag.ToString());
        }
        private void btncancelsave_Click(object sender, EventArgs e)
        {
            try
            {
                binding[tablindex(((Button)sender).Tag.ToString())].CancelEdit();
                tabl(((Button)sender).Tag.ToString()).RejectChanges();
            }
            catch
            {
                MessageBox.Show("Cancel failed.");
            }
            CloseTab(tabBody.TabPages["Add" + ((Button)sender).Tag.ToString()]);
            OpenTab(tabTemplate.TabPages[((Button)sender).Tag.ToString()], ((Button)sender).Tag.ToString());
            DataGridView grid = SetGrid(((Button)sender).Tag.ToString());
        }
        //calc buttons*****
        private void btnopencalcfields_Click(object sender, EventArgs e)
        {
            OpenTab(tabTemplate.TabPages[((Button)sender).Tag.ToString()], ((Button)sender).Text);
            ClearControls(pnl:pnlCalculatedFields_Body);
            CreateCalcTab(tabBody.TabPages["CalculatedFields"]);
        }
        private void btncancelcalcfields_Click(object sender, EventArgs e)
        {
            cmbCalcFields.Items.Clear();
            CloseTab(tabBody.TabPages[((Button)sender).Tag.ToString()]);
        }
        private void btnAddCalcField_Click(object sender, EventArgs e)
        {
            if (cmbCalcFields.Visible == true && cmbCalcFields.Text!="")
            {
                var split = cmbCalcFields.Text.Split(' ');
                string col = split[0];
                string table = split[1].TrimStart('(').TrimEnd(')');
                bool exists = false;

                var ctrllist = SearchControls(pnl: pnlCalculatedFields_Body, filter: "name");

                foreach (Control ctrl in ctrllist)
                {
                    var split2 = ctrl.Text.Split(' ');
                    string col2 = split2[0];
                    string table2 = split2[1].TrimStart('(').TrimEnd(')');
                    if (col2 == col && table2==table)
                    {
                        exists = true;
                    }
                }
                if (!exists)
                {
                    int xloc = int.Parse(txtXLoc.Text);
                    int yloc = int.Parse(txtYLoc.Text);
                    int xspace = int.Parse(txtXSpace.Text);
                    int yspace = int.Parse(txtYSpace.Text);
                    int wbox = int.Parse(txtWBox.Text);
                    int hbox = int.Parse(txtHBox.Text);
                    int count = int.Parse(txtCount.Text);

                    Label newlbl = CtrlLabel("name" + count.ToString().PadLeft(2, '0'), cmbCalcFields.Text, autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                    pnlCalculatedFields_Body.Controls.Add(newlbl);
                    Button newbtn = CtrlButton("remv" + count.ToString().PadLeft(2, '0'), "Remove", autosize: false, width: wbox, height: hbox, xloc: xloc + xspace, yloc: yloc, click: btnremovecalcfield);
                    pnlCalculatedFields_Body.Controls.Add(newbtn);
                    newlbl.BringToFront();
                    yloc += yspace;
                    for (int i = 1; i <= 4; i++)
                    {
                        ComboBox newcmb = CtrlCombo("type" + count.ToString().PadLeft(2, '0') + i.ToString().PadLeft(2, '0'),
                            new List<string> {"None", "Date (yyyymmdd)", "Date (yyyymmddhhmmss)", "Random number (2)", "Random number (3)",
                                        "Random number (4)", "Random number (5)", "Sequential number (2)", "Sequential number (3)",
                                        "Sequential number (4)", "Sequential number (5)", "Random letter (2)", "Random letter (3)",
                                        "Random letter (4)", "Random letter (5)", "Sequential letter (2)", "Sequential letter (3)",
                                        "Sequential letter (4)", "Sequential letter (5)" }
                            , false, hbox, wbox, xloc, yloc);
                        yloc += yspace;
                        TextBox newtxt = CtrlText("valu" + count.ToString().PadLeft(2, '0') + i.ToString().PadLeft(2, '0'), autosize: false, width: wbox, height: hbox, xloc: xloc, yloc: yloc);
                        xloc += xspace;
                        yloc -= yspace;
                        pnlCalculatedFields_Body.Controls.Add(newcmb);
                        newtxt.BringToFront();
                        pnlCalculatedFields_Body.Controls.Add(newtxt);
                        newtxt.BringToFront();
                        newcmb.Text = "None";
                        newtxt.Text = "";
                    }
                    count++;
                    yloc += (yspace*2);
                    txtYLoc.Text = yloc.ToString();
                    txtXSpace.Text = xspace.ToString();
                    txtYSpace.Text = yspace.ToString();
                    txtWBox.Text = wbox.ToString();
                    txtHBox.Text = hbox.ToString();
                    txtCount.Text = count.ToString();
                    cmbCalcFields.Visible = false;
                    btnAddCalcField.Text = "Add calc field";
                }
            }
            else if(cmbCalcFields.Visible==false)
            {
                cmbCalcFields.Items.Clear();
                cmbCalcFields.Text = "";
                cmbCalcFields.Visible = true;
                btnAddCalcField.Text = "Add";
                foreach (var table in alltables)
                {
                    if (table!= "CalculatedFields")
                    {
                        foreach (DataColumn col in tabl(table).Columns)
                        {
                            if (col.AutoIncrement != true)
                            {
                                bool exists = false;
                                var ctrllist = SearchControls(pnlCalculatedFields_Body, filter: "name");
                                foreach (var ctrl in ctrllist)
                                {
                                    var split2 = ctrl.Text.Split(' ');
                                    string col2 = split2[0];
                                    string table2 = split2[1].TrimStart('(').TrimEnd(')');
                                    if (col2 == col.ColumnName && table2 == table)
                                    {
                                        exists = true;
                                    }
                                }
                                if (!exists)
                                {
                                    cmbCalcFields.Items.Add(col.ColumnName + " (" + table + ")");
                                }
                            }
                        } 
                    }
                }
            }
            else
            {
                cmbCalcFields.Items.Clear();
                cmbCalcFields.Visible = false;
                btnAddCalcField.Text = "Add calc field";
            }
        }
        private void btnSaveCalcFields_Click(object sender, EventArgs e)
        {
            cmbCalcFields.Items.Clear();
            cmbCalcFields.Visible = false;
            var ctrllist = pnlCalculatedFields_Body.Controls;
            foreach (Control ctrl in ctrllist)
            {
                if (ctrl.Name.Substring(0, 4) == "name")
                {
                    try
                    {
                        string count = ctrl.Name.Substring(4, 2);
                        var split = ctrl.Text.Split(' ');
                        string col = split[0];
                        string table = split[1].TrimStart('(').TrimEnd(')');
                        var search = ds.Tables["CalculatedFields"].AsEnumerable().Where(r => r["Fieldname"].ToString() == col && r["Tablename"].ToString() == table).ToList();
                        ComboBox cmb1 = pnlCalculatedFields_Body.Controls["type" + count + "01"] as ComboBox;
                        ComboBox cmb2 = pnlCalculatedFields_Body.Controls["type" + count + "02"] as ComboBox;
                        ComboBox cmb3 = pnlCalculatedFields_Body.Controls["type" + count + "03"] as ComboBox;
                        ComboBox cmb4 = pnlCalculatedFields_Body.Controls["type" + count + "04"] as ComboBox;
                        TextBox val1 = pnlCalculatedFields_Body.Controls["valu" + count + "01"] as TextBox;
                        TextBox val2 = pnlCalculatedFields_Body.Controls["valu" + count + "02"] as TextBox;
                        TextBox val3 = pnlCalculatedFields_Body.Controls["valu" + count + "03"] as TextBox;
                        TextBox val4 = pnlCalculatedFields_Body.Controls["valu" + count + "04"] as TextBox;
                        if (search.Count == 0)
                        {
                            DataRow newrow = ds.Tables["CalculatedFields"].NewRow();
                            newrow["Fieldname"] = col;
                            newrow["Tablename"] = table;
                            newrow["Type1"] = cmb1.Text;
                            newrow["Val1"] = val1.Text;
                            newrow["Type2"] = cmb2.Text;
                            newrow["Val2"] = val2.Text;
                            newrow["Type3"] = cmb3.Text;
                            newrow["Val3"] = val3.Text;
                            newrow["Type4"] = cmb4.Text;
                            newrow["Val4"] = val4.Text;
                            ds.Tables["CalculatedFields"].Rows.Add(newrow);
                        }
                        else
                        {
                            search[0]["Type1"] = cmb1.Text;
                            search[0]["Val1"] = val1.Text;
                            search[0]["Type2"] = cmb2.Text;
                            search[0]["Val2"] = val2.Text;
                            search[0]["Type3"] = cmb3.Text;
                            search[0]["Val3"] = val3.Text;
                            search[0]["Type4"] = cmb4.Text;
                            search[0]["Val4"] = val4.Text;
                        }
                        SaveData();
                    }
                    catch (Exception ex) { }
                }
            }
            CloseTab(tabBody.TabPages[((Button)sender).Tag.ToString()]);
        }
        private void btnremovecalcfield(object sender, EventArgs e)
        {
            try
            {
                Button btn = ((Button)sender);
                string count = btn.Name.Substring(4, 2);
                string name = (pnlCalculatedFields_Body.Controls.Find("name" + count, false).ToList().First() as Label).Text;
                var split = name.Split(' ');
                string col = split[0];
                string table = split[1].TrimStart('(').TrimEnd(')');
                DataRow remove = tabl("CalculatedFields").AsEnumerable().Where(r => r["Fieldname"].ToString() == col && r["Tablename"].ToString() == table).First();
                remove.Delete();
                try
                {
                    SaveData();
                }
                catch { }
                RefreshCalcFields();
            }
            catch
            {
                RefreshCalcFields();
            }
        }
        //ON CHANGE
        private void Addrecord_FKchange(object sender, EventArgs e)
        {
            try
            {
                ComboBox cmb = ((ComboBox)sender);
                string name = "txt" + cmb.Name.Substring(3, cmb.Name.Length - 3); ;
                TextBox txt = tabBody.Controls.Find(name, true).ToList().First() as TextBox;
                txt.Text = cmb.SelectedValue.ToString();
                txt.Focus();
                cmb.Focus();
            }
            catch { }
        }
//CREATE CONTROLS
        private Label CtrlLabel(string name, string text, bool autosize = true, int? height = null, int? width = null,
            int? xloc = 0, int? yloc = 0)
        {
            Label newctrl = new Label();
            newctrl.Name = name;
            newctrl.Text = text;
            newctrl.AutoSize = autosize;
            newctrl.Location = new Point((int)xloc, (int)yloc);
            if (height != null)
            {
                newctrl.Height = (int)height;
            }
            if (width != null)
            {
                newctrl.Width = (int)width;
            }
            return newctrl;
        }
        private TextBox CtrlText(string name, string text = "", bool autosize = true, int? height = null, int? width = null,
            int? xloc = 0, int? yloc = 0, bool enabled = true, BindingSource datasrc = null, string dsp = "", string val = "Text"
            , bool visible = true)
        {
            TextBox newctrl = new TextBox();
            newctrl.Name = name;
            newctrl.Text = text;
            if (datasrc != null)
            {
                newctrl.DataBindings.Add(val, datasrc, dsp, false, DataSourceUpdateMode.OnPropertyChanged);
            }
            newctrl.AutoSize = autosize;
            newctrl.Location = new Point((int)xloc, (int)yloc);
            newctrl.Enabled = enabled;
            if (height != null)
            {
                newctrl.Height = (int)height;
            }
            if (width != null)
            {
                newctrl.Width = (int)width;
            }
            newctrl.Visible = visible;

            return newctrl;
        }
        private ComboBox CtrlCombo(string name, List<string> items = null, bool autosize = true, int? height = null, int? width = null,
            int? xloc = 0, int? yloc = 0, bool enabled = true, BindingSource datasrc = null, string dsp = "", string val = "", int selected = 0
            , EventHandler onclick = null)
        {
            ComboBox newctrl = new ComboBox();
            newctrl.Name = name;
            if (items != null)
            {
                newctrl.Items.AddRange(items.ToArray());
            }
            if (datasrc != null)
            {
                newctrl.DataSource = datasrc;
                newctrl.DisplayMember = dsp;
                newctrl.ValueMember = val;
            }
            newctrl.DropDownStyle = ComboBoxStyle.DropDownList;
            newctrl.AutoSize = autosize;
            newctrl.Location = new Point((int)xloc, (int)yloc);
            newctrl.Enabled = enabled;
            newctrl.SelectedValueChanged += onclick;
            if (height != null)
            {
                newctrl.Height = (int)height;
            }
            if (width != null)
            {
                newctrl.Width = (int)width;
            }

            return newctrl;
        }
        private DataGridView CtrlGrid(string name, DockStyle dockstyle = DockStyle.Fill, int? height = null, int? width = null,
            int? xloc = 0, int? yloc = 0, bool enabled = true, DataTable datasrc = null)
        {
            DataGridView newctrl = new DataGridView();
            newctrl.Name = name;
            if (datasrc != null)
            {
                newctrl.DataSource = datasrc;
            }
            if (dockstyle == DockStyle.Fill)
            {
                newctrl.Dock = dockstyle;
            }
            else
            {
                newctrl.Location = new Point((int)xloc, (int)yloc);
                newctrl.Height = (int)height;
                newctrl.Width = (int)width;
            }
            newctrl.Enabled = enabled;
            if (height != null)
            {
                newctrl.Height = (int)height;
            }
            if (width != null)
            {
                newctrl.Width = (int)width;
            }
            return newctrl;
        }
        private Button CtrlButton(string name, string text, bool autosize = true, int? height = null, int? width = null,
            int? xloc = 0, int? yloc = 0, EventHandler click = null)
        {
            Button newctrl = new Button();
            newctrl.Name = name;
            newctrl.Text = text;
            newctrl.AutoSize = autosize;
            newctrl.Location = new Point((int)xloc, (int)yloc);
            if (height != null)
            {
                newctrl.Height = (int)height;
            }
            if (width != null)
            {
                newctrl.Width = (int)width;
            }
            newctrl.Click += click;
            return newctrl;
        }

        private void btndeleterecord_Click(object sender, EventArgs e)
        {
            string table = ((Button)sender).Tag.ToString();
            DataRow row = (binding[tablindex(table)].Current as DataRowView).Row;
            var confirm = MessageBox.Show("Are you sure you would like to delete the current " + table + " record?", "Confirm delete", MessageBoxButtons.YesNo);
            if (confirm==DialogResult.Yes)
            {
                row.Delete();
                SaveData();
            }
        }
    }
}
