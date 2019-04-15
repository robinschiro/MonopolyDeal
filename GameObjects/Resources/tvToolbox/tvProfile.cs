using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Xml;


namespace tvToolbox
{
    /// <summary>
    /// Fetches a resource file by name (asResourceName) from the current executable.
    /// </summary>
    public delegate void FetchResourceToDisk(String asResourceName);

    /// <summary>
    /// Fetches a resource file by name (asResourceName) from the current executable
    /// using asNamespace. The resulting file is written to asPathFile.
    /// </summary>
    public delegate void FetchResourceToDisk2(String asNamespace, String asResourceName, String asPathFile);

    /// <summary>
    /// Supported tvProfile data types.
    /// </summary>
    public enum tvProfileSupportedDataTypes
    {
         Bool
        ,DateTime
        ,Double
        ,Integer
        ,String
    }

    /// <summary>
    /// Default profile file actions specify how defaults are handled at runtime.
    /// </summary>
    public enum tvProfileDefaultFileActions
    {
        /// <summary>
        /// Automatically load the default profile file during application
        /// startup. Also, save the default profile file without prompts
        /// whenever new keys are automatically added (ie. whenever new
        /// keys with default values are referenced in the application
        /// code for the first time).
        /// </summary>
        AutoLoadSaveDefaultFile = 1,

        /// <summary>
        /// Do not use a default profile file.
        /// </summary>
        NoDefaultFile
    };

    /// <summary>
    /// Profile file create actions specify how to handle files that don't yet exist.
    /// </summary>
    public enum tvProfileFileCreateActions
    {
        /// <summary>
        /// Prompt the user to create the application's profile file (either
        /// the default profile file or a given alternative, created only if
        /// it doesn't already exist). Default settings will be automatically
        /// added to the profile file as they are encountered during the normal
        /// course of the application run.
        /// </summary>
        PromptToCreateFile = 1,

        /// <summary>
        /// Do not automatically create a profile file.
        /// </summary>
        NoFileCreate,

        /// <summary>
        /// Automatically create a profile file without user prompts
        /// (ie. "no questions asked").
        /// </summary>
        NoPromptCreateFile
    };

    /// <summary>
    /// Profile load actions specify how to handle items as they are loaded.
    /// </summary>
    public enum tvProfileLoadActions
    {
        /// <summary>
        /// Append all loaded items to the end of the profile.
        /// Duplicate keys are OK.
        /// </summary>
        Append = 1,

        /// <summary>
        /// Merge all loaded items into the profile. Matching items
        /// (by key) found in the profile will be replaced. Items
        /// to be loaded that do not match any current items will be appended
        /// to the end of the profile. Note: "*" as well as formal regular
        /// expressions can be used to match multiple keys. This means that
        /// long keys can be referenced on the command line with "*"
        /// (eg. "-Auto*" matches "-AutoRun" and "-AutoPlay"). Keys with
        /// wildcards will not be appended.
        /// </summary>
        Merge,

        /// <summary>
        /// Clear the profile, then append all loaded items.
        /// </summary>
        Overwrite
    };


    /// <summary>
    /// <p>
    /// This class provides a simple yet flexible interface to
    /// a collection of persistent application level properties.
    /// </p>
    /// <p>
    /// The key feature of tvProfile is its seamless integration of
    /// file based properties with command line arguments and switches.
    /// </p>
    /// <p>
    /// Application parameters (eg. <see langword='-Key1="value one" -Key2=abc
    /// -Key3=3'/>) can be intermixed with switches (eg. <see langword='-Switch'
    /// />) either in the application's profile file or on the command
    /// line or both. A switch is just shorthand for a boolean parameter. For
    /// example, <see langword='-Switch'/> is equivalent to <see langword=
    /// '-Switch=True'/>.
    /// </p>
    /// <p>
    /// tvProfile command line switches / parameters typically override
    /// corresponding keys found in a profile file.
    /// </p>
    /// <p>
    /// Each application has a default plain text profile file named
    /// AppName.exe.txt, where AppName.exe is the executable filename of
    /// the application. The default profile file will always be found
    /// in the same folder as the application executable file. In fact,
    /// if it doesn't already exist in its default location, the application
    /// will automatically create the default profile file and automatically
    /// populate it with default values.
    /// </p>
    /// <p>
    /// As an alternative to the default delimited "command line" profile
    /// file format, XML can be used instead by passing a boolean to the
    /// class constructor or by setting <see cref="bUseXmlFiles"/>.
    /// </p>
    /// <p>
    /// See <see langword='Remarks'/> for additional options.
    /// </p>
    /// <p>
    /// Author:     George Schiro (GeoCode@Schiro.name)
    /// </p>
    /// <p>
    /// Version:    2.1
    /// Copyright:  1996 - 2121
    /// </p>
    /// </summary>
    /// <remarks>
    /// The following are all of the "predefined" profile parameters (ie. those
    /// that exist for every profile):
    ///
    /// <list type="table">
    /// <listheader>
    ///     <term>Parameter</term>
    ///     <description>Description</description>
    /// </listheader>
    /// <item>
    ///     <term>-ini="path/file"</term>
    ///     <description>
    ///     A profile file location used to override the default profile file.
    ///     Alternatively, if the first argument passed on the command line is
    ///     actually a file location (ie. a path/file specification) that refers
    ///     to an existing file, that file will be assumed to be a profile file
    ///     to override the default.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-File="path/file"</term>
    ///     <description>
    ///     The first command line argument passed to the application, if a
    ///     profile file location has otherwise been provided. In other words,
    ///     if there is already an "-ini" key passed on the command line
    ///     (after the first argument), then any file passed as the first
    ///     argument (a file that actually exists) will be added to the
    ///     profile as <see langword='-File="path/file"' />.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-NoCreate</term>
    ///     <description>
    ///     False by default. This and "-ini" (with its alias -ProfileFile) are
    ///     the only parameters that do not appear in a profile file. It is only
    ///     passed on the application command line. When the switch <see langword='-NoCreate'/>
    ///     appears on the command line, users are not prompted to create a profile
    ///     file. If a default profile does not exist at runtime, one will not
    ///     be created and default values will not be persisted. This option only
    ///     makes sense when the application needs to run with its original
    ///     "built-in" default values only.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-ProfileFile="path/file"</term>
    ///     <description>
    ///     A file used to override the default profile file (same as <see langword='-ini' />).
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-SaveProfile</term>
    ///     <description>
    ///     True by default (after the profile has been loaded from
    ///     a text file). Set this false to prevent automated changes
    ///     to the profile file.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-SaveSansCmdLine</term>
    ///     <description>
    ///     True by default. Set this false to prevent automated changes
    ///     to the profile file after command line merges have occured.
    ///     When true, everything but command line keys will be saved.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-ShowProfile</term>
    ///     <description>
    ///     False by default. Display the contents of the profile in "command line"
    ///     format during application startup. This is sometimes helpful for
    ///     debugging purposes.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term>-XML_Profile</term>
    ///     <description>
    ///     False by default. Set this true to convert the profile file to XML
    ///     format and to maintain it that way. Set it false to convert it
    ///     back to line delimited "command line" format.
    ///     </description>
    /// </item>
    /// </list>
    ///
    /// </remarks>
    public class tvProfile : ArrayList
    {
        #region "Constructors, Statics and Overridden or Augmented Members"

        /// <summary>
        /// Initializes a new instance of the tvProfile class.
        /// </summary>
        public tvProfile() : this(
                                      tvProfileDefaultFileActions.NoDefaultFile
                                    , tvProfileFileCreateActions.NoFileCreate
                                    )
        {
        }

        /// <summary>
        /// This is the main constructor.
        ///
        /// This constructor (or one of its shortcuts) is typically used at
        /// the top of an application during initialization.
        ///
        /// The profile is first initialized using the aeDefaultFileAction
        /// enum. Then any command line arguments (typically passed from
        /// the environment) are merged into the profile. This way command
        /// line arguments override properties in the profile file.
        ///
        /// Initialization of the profile is done by first loading
        /// properties from an existing default profile file.
        ///
        /// The aeFileCreateAction enum enables a new profile file to be
        /// created (with or without prompting), if it doesn't already exist
        /// and then filled with default settings.
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        /// <param name="aeDefaultFileAction">
        /// This enum indicates how to handle automatic loading and saving
        /// of the default profile file.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the default profile file, if it doesn't already exist.
        /// </param>
        /// <param name="abUseXmlFiles">
        /// If true, XML file format will be used. If false, line delimited
        /// "command line" format will be used (the default format).
        /// </param>
        public tvProfile(

                  String[]                      asCommandLineArray
                , tvProfileDefaultFileActions   aeDefaultFileAction
                , tvProfileFileCreateActions    aeFileCreateAction
                , bool                          abUseXmlFiles
                )
        {
            this.sInputCommandLineArray = asCommandLineArray;
            this.eDefaultFileAction = aeDefaultFileAction;
            this.eFileCreateAction = aeFileCreateAction;
            this.bUseXmlFiles = abUseXmlFiles;

            this.ReplaceDefaultProfileFromCommandLine(asCommandLineArray);
            if ( this.bExit )
            {
                return;
            }

            // "bDefaultFileReplaced = True" means that a replacement profile file has been passed on
            // the command line. Consequently, no attempt to load the default profile file should be made.
            if ( !this.bDefaultFileReplaced && tvProfileDefaultFileActions.NoDefaultFile != aeDefaultFileAction )
            {
                this.Load(null, tvProfileLoadActions.Overwrite);
                if ( this.bExit )
                {
                    return;
                }

                this.LoadFromCommandLineArray(asCommandLineArray, tvProfileLoadActions.Merge);
            }

            bool    lbShowProfile = false;
                    if ( mbAddStandardDefaults || this.ContainsKey("-ShowProfile") )
                    {
                        lbShowProfile = this.bValue("-ShowProfile", false);
                    }

            if ( lbShowProfile )
            {
                if ( DialogResult.Cancel
                        == MessageBox.Show(this.sCommandLine(), this.sLoadedPathFile, MessageBoxButtons.OKCancel)
                        )
                {
                    this.bExit = true;
                }
            }
        }

        /// <summary>
        /// This is the main constructor used with abitrary XML documents.
        ///
        /// This constructor (or one of its shortcuts) is typically used at
        /// the top of an application during initialization.
        ///
        /// The profile is first initialized using the aeDefaultFileAction
        /// enum. Then any command line arguments (typically passed from
        /// the environment) are merged into the profile. This way command
        /// line arguments override properties in the profile file.
        ///
        /// Initialization of the profile is done by first loading
        /// properties from an existing default profile file.
        ///
        /// The aeFileCreateAction enum enables a new profile file to be
        /// created (with or without prompting), if it doesn't already exist
        /// and then filled with default settings.
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        /// <param name="aeDefaultFileAction">
        /// This enum indicates how to handle automatic loading and saving
        /// of the default profile file.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the default profile file, if it doesn't already exist.
        /// </param>
        /// <param name="asXmlFile">
        /// This is the path/file that contains the XML to load.
        /// </param>
        /// <param name="asXmlXpath">
        /// This is the Xpath into asXmlFile that contains the profile.
        /// </param>
        /// <param name="asXmlKeyKey">
        /// This is the "Key" key used to find name attributes in asXmlXpath.
        /// </param>
        /// <param name="asXmlValueKey">
        /// This is the "Value" key used to find value attributes in asXmlXpath.
        /// </param>
        public tvProfile(

                  String[]                      asCommandLineArray
                , tvProfileDefaultFileActions   aeDefaultFileAction
                , tvProfileFileCreateActions    aeFileCreateAction
                , String                        asXmlFile
                , String                        asXmlXpath
                , String                        asXmlKeyKey
                , String                        asXmlValueKey
                )
        {
            this.sInputCommandLineArray = asCommandLineArray;
            this.eDefaultFileAction = aeDefaultFileAction;
            this.eFileCreateAction = aeFileCreateAction;
            this.sXmlXpath = asXmlXpath;
            this.sXmlKeyKey = asXmlKeyKey;
            this.sXmlValueKey = asXmlValueKey;
            this.bUseXmlFiles = true;

            this.ReplaceDefaultProfileFromCommandLine(asCommandLineArray);
            if ( this.bExit )
            {
                return;
            }

            // "bDefaultFileReplaced = True" means that a replacement profile file has been passed on
            // the command line. Consequently, no attempt to load the default profile file should be made.
            if ( !this.bDefaultFileReplaced && tvProfileDefaultFileActions.NoDefaultFile != aeDefaultFileAction )
            {
                this.Load(asXmlFile, tvProfileLoadActions.Overwrite);
                if ( this.bExit )
                {
                    return;
                }

                this.LoadFromCommandLineArray(asCommandLineArray, tvProfileLoadActions.Merge);
            }

            bool    lbShowProfile = false;
                    if ( mbAddStandardDefaults || this.ContainsKey("-ShowProfile") )
                    {
                        lbShowProfile = this.bValue("-ShowProfile", false);
                    }

            if ( lbShowProfile )
            {
                if ( DialogResult.Cancel
                        == MessageBox.Show(this.sCommandLine(), this.sLoadedPathFile, MessageBoxButtons.OKCancel)
                        )
                {
                    this.bExit = true;
                }
            }
        }

        /// <summary>
        /// This constructor is typically used to load or create a new
        /// profile file separate from the default profile file for the
        /// application.
        /// </summary>
        /// <param name="asPathFile">
        /// This is the location of the profile file to be loaded.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the profile file to be loaded, if it doesn't already exist.
        /// </param>
        /// <param name="abUseXmlFiles">
        /// If true, XML file format will be used. If false, line delimited
        /// "command line" format will be used (the default format).
        /// </param>
        public tvProfile(

                  String                        asPathFile
                , tvProfileFileCreateActions    aeFileCreateAction
                , bool                          abUseXmlFiles
                )
                : this()
        {
            this.eFileCreateAction = aeFileCreateAction;
            this.bUseXmlFiles = abUseXmlFiles;

            this.Load(asPathFile, tvProfileLoadActions.Overwrite);
        }

        /// <summary>
        /// This constructor is the shortcut to the main constructor
        /// using the default profile file format.
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        /// <param name="aeDefaultFileAction">
        /// This enum indicates how to handle automatic loading and saving
        /// of the default profile file.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the default profile file, if it doesn't already exist.
        /// </param>
        public tvProfile(

                  String[]                      asCommandLineArray
                , tvProfileDefaultFileActions   aeDefaultFileAction
                , tvProfileFileCreateActions    aeFileCreateAction
                )
                : this(   asCommandLineArray
                        , aeDefaultFileAction
                        , aeFileCreateAction
                        , false
                        )
        {
        }

        /// <summary>
        /// This is the minimal profile constructor.
        ///
        /// This constructor is typically used to create an empty profile
        /// object to be populated manually (ie. with <see cref="Add(String, Object)"/> calls).
        ///
        /// It is also used in lieu of the main constructor when command
        /// line overrides are not permitted.
        ///
        /// The default constructor is a shortcut to this one using the arguments
        /// "NoDefaultFile" and "NoFileCreate".
        /// </summary>
        /// <param name="aeDefaultFileAction">
        /// This enum indicates how to handle automatic loading and saving
        /// of the default profile file.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the default profile file, if it doesn't already exist.
        /// </param>
        public tvProfile(

                  tvProfileDefaultFileActions aeDefaultFileAction
                , tvProfileFileCreateActions aeFileCreateAction
                )
        {
            this.eDefaultFileAction = aeDefaultFileAction;
            this.eFileCreateAction = aeFileCreateAction;

            if ( tvProfileDefaultFileActions.AutoLoadSaveDefaultFile == aeDefaultFileAction )
            {
                this.Load(this.sDefaultPathFile, tvProfileLoadActions.Overwrite);
            }
            else
            {
                this.bAddStandardDefaults = false;
            }
        }

        /// <summary>
        /// This constructor initializes a profile object from a command line
        /// string (eg. <see langword='-Key1="value one" -Switch1 -Key2=2'/>)
        /// rather than from a string array.
        ///
        /// This is handy for building profiles from simple strings passed
        /// from a database, from other profiles or from strings embedded
        /// in the body of an application.
        /// </summary>
        /// <param name="asCommandLine">
        /// This string (not a string array) should contain a command line
        /// of <see langword='-key="value"'/> pairs.
        /// </param>
        public tvProfile(String asCommandLine) : this()
        {
            this.sInputCommandLine = asCommandLine;

            this.LoadFromCommandLine(asCommandLine, tvProfileLoadActions.Overwrite);
        }

        /// <summary>
        /// This constructor is typically used to load or create a new
        /// profile file separate from the default profile file for the
        /// application (using the default profile file format).
        /// </summary>
        /// <param name="asPathFile">
        /// This is the location of the profile file to be loaded.
        /// </param>
        /// <param name="aeFileCreateAction">
        /// This enum indicates how to handle the automatic creation of
        /// the profile file to be loaded, if it doesn't already exist.
        /// </param>
        public tvProfile(

                  String                        asPathFile
                , tvProfileFileCreateActions    aeFileCreateAction
                )
                : this(   asPathFile
                        , aeFileCreateAction
                        , false
                        )
        {
        }

        /// <summary>
        /// This constructor is typically used to load a profile file 
        /// separate from the default profile file for the application.
        /// The profile file must already exist. It will not be created.
        /// </summary>
        /// <param name="asPathFile">
        /// This is the location of the profile file to be loaded.
        /// </param>
        /// <param name="abUseXmlFiles">
        /// If true, XML file format will be used. If false, line delimited
        /// "command line" format will be used (the default format).
        /// </param>
        public tvProfile(

                  String    asPathFile
                , bool      abUseXmlFiles
                )
                : this(   asPathFile
                        , tvProfileFileCreateActions.NoFileCreate
                        , abUseXmlFiles
                        )
        {
        }

        /// <summary>
        /// This constructor is the shortcut to the main XML constructor
        /// using the most typical options (ie. "AutoLoadSaveDefaultFile"
        /// and "PromptToCreateFile").
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        /// <param name="asXmlFile">
        /// This is the path/file that contains the XML to load.
        /// </param>
        /// <param name="asXmlXpath">
        /// This is the Xpath into asXmlFile that contains the profile.
        /// </param>
        /// <param name="asXmlKeyKey">
        /// This is the "Key" key used to find name attributes in asXmlXpath.
        /// </param>
        /// <param name="asXmlValueKey">
        /// This is the "Value" key used to find value attributes in asXmlXpath.
        /// </param>
        public tvProfile(

                  String[]  asCommandLineArray
                , String    asXmlFile
                , String    asXmlXpath
                , String    asXmlKeyKey
                , String    asXmlValueKey
                )
                : this(   asCommandLineArray
                        , tvProfileDefaultFileActions.AutoLoadSaveDefaultFile
                        , tvProfileFileCreateActions.PromptToCreateFile
                        , asXmlFile
                        , asXmlXpath
                        , asXmlKeyKey
                        , asXmlValueKey
                        )
        {
        }

        /// <summary>
        /// This constructor is the shortcut to the main constructor
        /// using the most typical options (ie. "AutoLoadSaveDefaultFile"
        /// and "PromptToCreateFile").
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        /// <param name="abUseXmlFiles">
        /// If true, XML file format will be used. If false, line delimited
        /// "command line" format will be used (the default format).
        /// </param>
        public tvProfile(

                  String[]  asCommandLineArray
                , bool      abUseXmlFiles
                )
                : this(   asCommandLineArray
                        , tvProfileDefaultFileActions.AutoLoadSaveDefaultFile
                        , tvProfileFileCreateActions.PromptToCreateFile
                        , abUseXmlFiles
                        )
        {
        }

        /// <summary>
        /// This constructor is the shortcut to the main constructor
        /// using the most typical options (ie. "AutoLoadSaveDefaultFile",
        /// "PromptToCreateFile" and the default profile file format).
        /// </summary>
        /// <param name="asCommandLineArray">
        /// This string array is typically passed from the environment to the
        /// running application (eg. from Environment.GetCommandLineArgs() ).
        /// It is merged with the default profile file or any other profile
        /// found within the list of command line arguments.
        /// </param>
        public tvProfile(

                String[] asCommandLineArray
                )
                : this(   asCommandLineArray
                        , tvProfileDefaultFileActions.AutoLoadSaveDefaultFile
                        , tvProfileFileCreateActions.PromptToCreateFile
                        , false
                        )
        {
        }


        /// <summary>
        /// This static factory method creates the global tvProfile object
        /// (ie. if it doesn't already exist).
        /// </summary>
        /// <returns>The global tvProfile object.</returns>
        public static tvProfile oGlobal()
        {
            if ( null == goGlobal )
            {
                goGlobal = new tvProfile(Environment.GetCommandLineArgs());
            }

            return goGlobal;
        }
        private static tvProfile goGlobal;


        #region "SortedList Member Emulation and Other Overrides"

        // The following methods don't necessarily augment or override ArrayList
        // members. They allow this class to emulate SortedList with added support
        // for duplicate keys. Inherit from SortedList and comment out these members.
        // Then this class will behave like SortedList.

        /// <summary>
        /// Adds the given "key/value" pair to the end of the profile.
        /// </summary>
        /// <param name="asKey">
        /// The key string stored with the value object.
        /// </param>
        /// <param name="aoValue">
        /// The value (as a generic object) to add.
        /// </param>
        public void Add(String asKey, Object aoValue)
        {
            DictionaryEntry loEntry = new DictionaryEntry();
                            loEntry.Key = asKey;
                            loEntry.Value = aoValue;

            base.Add(loEntry);
        }

        /// <summary>
        /// Overrides the base <see langword='Add(Object)'/> method in ArrayList.
        /// It throws an exception if the given object is not a DictionaryEntry.
        /// </summary>
        /// <param name="aoDictionaryEntry">
        /// The DictionaryEntry object to add to the collection.
        /// </param>
        /// <returns>
        /// The System.Collections.ArrayList index at which the object has been added.
        /// </returns>
        public override int Add(Object aoDictionaryEntry)
        {
            if ( typeof(DictionaryEntry) != aoDictionaryEntry.GetType() )
            {
                throw new InvalidAddType();
            }
            else
            {
                return base.Add(aoDictionaryEntry);
            }
        }

        /// <summary>
        /// Returns true if the given object value is found in the profile.
        /// </summary>
        /// <param name="aoValue">
        /// The object to look for. The objects searched will be converted
        /// to strings if aoValue is a string. "*" or a regular expression
        /// may be included in aoValue.
        /// </param>
        /// <returns>
        /// True if found, false if not.
        /// </returns>
        public override bool Contains(Object aoValue)
        {
            if ( "".GetType() == aoValue.GetType() )
            {
                String lsValue = aoValue.ToString();

                if ( mbUseLiteralsOnly )
                {
                    foreach ( DictionaryEntry loEntry in this )
                    {
                        if ( loEntry.Value.ToString() == lsValue )
                        {
                            return true;
                        }
                    }
                }
                else
                {
                    foreach ( DictionaryEntry loEntry in this )
                    {
                        if ( Regex.IsMatch(loEntry.Value.ToString(), sExpression(lsValue), RegexOptions.IgnoreCase) )
                        {
                            return true;
                        }
                    }
                }
            }
            else
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( loEntry.Value == aoValue )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the given literal string value is found in the profile.
        /// </summary>
        /// <param name="asValue">
        /// The string value to look for. Regular expressions are ignored
        /// (ie. all characters in asValue are treated as literals).
        /// </param>
        /// <returns>
        /// True if found, false if not.
        /// </returns>
        public bool ContainsLiteral(String asValue)
        {
            foreach ( DictionaryEntry loEntry in this )
            {
                if ( loEntry.Value.ToString() == asValue )
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if the given key string is found in the profile.
        /// </summary>
        /// <param name="asKey">
        /// The key string to look for. "*" or a regular expression may be
        /// included.
        /// </param>
        /// <returns>
        /// True if found, false if not.
        /// </returns>
        public bool ContainsKey(String asKey)
        {
            foreach ( DictionaryEntry loEntry in this )
            {
                if ( mbUseLiteralsOnly )
                {
                    if ( loEntry.Key.ToString() == asKey )
                    {
                        return true;
                    }
                }
                else
                {
                    if ( Regex.IsMatch(loEntry.Key.ToString(), sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The zero-based index of the first entry in the profile with a key
        /// that matches asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string to look for. "*" or a regular expression may be
        /// included.
        /// </param>
        /// <returns>
        /// The zero-based index of the entry found. -1 is returned if no entry is found.
        /// </returns>
        public int IndexOfKey(String asKey)
        {
            if ( mbUseLiteralsOnly )
            {
                for ( int i = 0; i <= this.Count - 1; i++ )
                {
                    if ( ((DictionaryEntry) base[i]).Key.ToString() == asKey )
                    {
                        return i;
                    }
                }
            }
            else
            {
                for ( int i = 0; i <= this.Count - 1; i++ )
                {
                    if ( Regex.IsMatch(((DictionaryEntry) base[i]).Key.ToString(), sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// The zero-based index of the first entry in the profile with a value
        /// that matches asValue.
        /// </summary>
        /// <param name="asValue">
        /// The string value to look for. The objects searched will be converted
        /// to strings. "*" or a regular expression may be included in asValue.
        /// </param>
        /// <returns>
        /// The zero-based index of the entry found. -1 is returned if no entry is found.
        /// </returns>
        public int IndexOf(String asValue)
        {
            if ( mbUseLiteralsOnly )
            {
                for ( int i = 0; i <= this.Count - 1; i++ )
                {
                    if ( ((DictionaryEntry) base[i]).Value.ToString() == asValue )
                    {
                        return i;
                    }
                }
            }
            else
            {
                for ( int i = 0; i <= this.Count - 1; i++ )
                {
                    if ( Regex.IsMatch(((DictionaryEntry) base[i]).Value.ToString(), sExpression(asValue), RegexOptions.IgnoreCase) )
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary>
        /// This is the string indexer for the class.
        /// </summary>
        /// <param name="asKey">
        /// The key string to look for. "*" or a regular expression may be
        /// included.
        /// </param>
        /// <returns>
        /// Gets or sets the object value of the first entry found that matches asKey.
        /// </returns>
        public Object this[String asKey]
        {
            get
            {
                int liIndex = this.IndexOfKey(asKey);

                if ( -1 == liIndex )
                {
                    return null;
                }
                else
                {
                    return this[liIndex];
                }
            }
            set
            {
                int liIndex = this.IndexOfKey(asKey);

                if ( -1 == liIndex )
                {
                    this.Add(asKey, value);
                }
                else
                {
                    this.SetByIndex(liIndex, value);
                }
            }
        }

        /// <summary>
        /// This is the integer indexer for the class.
        /// </summary>
        /// <param name="aiIndex">
        /// The zero-based index to look for.
        /// </param>
        /// <returns>
        /// Gets or sets the object value of the entry found at the given zero-based index position.
        /// </returns>
        public override Object this[int aiIndex]
        {
            get
            {
                return ((DictionaryEntry) base[aiIndex]).Value;
            }
            set
            {
                this.SetByIndex(aiIndex, value);
            }
        }

        /// <summary>
        /// Removes zero, one or many entries in the profile with keys
        /// that match the given asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string to look for. "*" or a regular expression may be
        /// included.
        /// </param>
        public void Remove(String asKey)
        {
            int liIndex;

            do
            {
                liIndex = this.IndexOfKey(asKey);

                if ( -1 != liIndex )
                {
                    this.RemoveAt(liIndex);
                }

            } while ( -1 != liIndex );
        }

        /// <summary>
        /// Sets the object at the given zero-based index position within the
        /// profile to the given object value. This method is called by the
        /// integer indexer for the class.
        /// </summary>
        /// <param name="aiIndex">
        /// The zero-based index to look for.
        /// </param>
        /// <param name="aoValue">
        /// The object value that is written to the entry at the given zero-based index position.
        /// </param>
        public void SetByIndex(int aiIndex, Object aoValue)
        {
            String lsKey = ((DictionaryEntry) base[aiIndex]).Key.ToString();

            base[aiIndex] = new DictionaryEntry(lsKey, aoValue);
        }

        /// <summary>
        /// Overrides base ToString() method.
        /// </summary>
        /// <returns>tvProfile contents in command-line block format.</returns>
        public override string ToString()
        {
            return this.sCommandBlock();
        }
        #endregion

        #endregion

        /// <summary>
        /// Returns true if the standard "built-in" profile defaults
        /// will be automatically added to the profile. This property
        /// will generally be true within the main constructors.
        /// </summary>
        public  bool  bAddStandardDefaults
        {
            get
            {
                return mbAddStandardDefaults;
            }
            set
            {
                mbAddStandardDefaults = value;
            }
        }
        private bool mbAddStandardDefaults = true;

        /// <summary>
        /// Returns true if the profile was instanced from a file loaded using
        /// the predefined parameter: <see langword='-ini="path/file"'/>
        /// (see <see cref="tvProfile"/> remarks). In other words, this property
        /// returns true if the application's default profile file was replaced.
        /// </summary>
        public  bool  bDefaultFileReplaced
        {
            get
            {
                return mbDefaultFileReplaced;
            }
            set
            {
                mbDefaultFileReplaced = value;
            }
        }
        private bool mbDefaultFileReplaced = false;

        /// <summary>
        /// Returns true if the profile file is maintained in a
        /// locked state while the profile object exists. This
        /// prevents overstepping access by external processes.
        /// 
        /// The default value of this property is true.
        /// </summary>
        public  bool  bEnableFileLock
        {
            get
            {
                return mbEnableFileLock;
            }
            set
            {
                mbEnableFileLock = value;

                if ( mbEnableFileLock )
                    this.bLockProfileFile(this.sActualPathFile);
                else
                    this.UnlockProfileFile();
            }
        }
        private bool mbEnableFileLock = true;

        /// <summary>
        /// Returns true if the user selected "Cancel" in response to a profile message.
        /// </summary>
        public  bool  bExit
        {
            get
            {
                return mbExit;
            }
            set
            {
                mbExit = value;

                if ( mbExit )
                {
                    this.bSaveEnabled = false;
                }
            }
        }
        private bool mbExit = false;

        /// <summary>
        /// Returns true if the profile's file was just created. It returns
        /// false if the profile file existed previously (at runtime).
        /// </summary>
        public  bool  bFileJustCreated
        {
            get
            {
                return mbFileJustCreated;
            }
            set
            {
                mbFileJustCreated = value;
            }
        }
        private bool mbFileJustCreated = false;

        /// <summary>
        /// Returns true after the application informs
        /// the profile object it has been fully loaded.
        /// </summary>
        public  bool  bAppFullyLoaded
        {
            get
            {
                return mbAppFullyLoaded;
            }
            set
            {
                mbAppFullyLoaded = value;

                if ( mbAppFullyLoaded && null != moAppLoadingWaitMsg )
                    mttvMessageBox.InvokeMember("Close", BindingFlags.InvokeMethod, null, moAppLoadingWaitMsg
                                                , null);
            }
        }
        private bool mbAppFullyLoaded = false;

        /// <summary>
        /// Returns true if the EXE is in a folder (or subfolder) with the same name.
        /// </summary>
        public bool bInOwnFolder
        {
            get
            {
                String lsPathOnly = Path.GetDirectoryName(this.sExePathFile);
                String lsFilnameOnly = Path.GetFileNameWithoutExtension(this.sExePathFile);

                return -1 != lsPathOnly.IndexOf(lsFilnameOnly);
            }
        }

        /// <summary>
        /// Returns true if the EXE is already in a typical installation folder
        /// (eg. "Program Files").
        /// </summary>
        public bool bInstalledAlready
        {
            get
            {
                String[] lsPathFragArray = new String[]{"\\Program Files\\"};

                foreach (String lsPathFrag in lsPathFragArray)
                {
                    if ( -1 != this.sExePathFile.IndexOf(lsPathFrag) )
                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// <p>
        /// Returns true if the <see cref="Save()"/> method is enabled. <see cref="Save()"/>
        /// is enabled after a text file is successfully loaded or a text file
        /// is saved with <c>Save(asPathFile)</c>.
        /// </p>
        /// <p>
        /// This property will be set false whenever anything is merged into a
        /// profile (eg. command line arguments) and <see cref="bSaveSansCmdLine"/>
        /// is false. In other words, command line arguments are not normally 
        /// written to a profile file. This behavior can be overridden in code 
        /// by setting this property to true after a merge.
        /// </p>
        /// <p>
        /// The predefined <see langword='-SaveProfile=false'/> switch can also
        /// be used to disable the <see cref="Save()"/> method manually
        /// (see <see cref="tvProfile"/> remarks).
        /// </p>
        /// </summary>
        public  bool  bSaveEnabled
        {
            get
            {
                if ( mbAddStandardDefaults || this.ContainsKey("-SaveProfile") )
                {
                    if ( mbSaveEnabled )
                        mbSaveEnabled = this.bValue("-SaveProfile", mbSaveEnabled);
                }

                return mbSaveEnabled;
            }
            set
            {
                mbSaveEnabled = value;
            }
        }
        private bool mbSaveEnabled = false;

        /// <summary>
        /// Returns true if all but command line merged keys will be saved to a profile
        /// file. The predefined <see langword='-SaveSansCmdLine=false'/> switch can be
        /// used to disable this behavior so that merged profiles are never saved (see 
        /// <see cref="tvProfile"/> remarks). In other words, if this property is set 
        /// false and command line arguments have been merged into the profile, the 
        /// <see cref="Save()"/> method will be disabled (unless overridden in code).
        /// </summary>
        public  bool  bSaveSansCmdLine
        {
            get
            {
                if ( mbAddStandardDefaults || this.ContainsKey("-SaveSansCmdLine") )
                    mbSaveSansCmdLine = this.bValue("-SaveSansCmdLine", mbSaveSansCmdLine);

                if ( mbSaveSansCmdLine && null == moInputCommandLineProfile )
                {
                    moInputCommandLineProfile = new tvProfile();
                    moInputCommandLineProfile.LoadFromCommandLineArray(this.msInputCommandLineArray, tvProfileLoadActions.Append);
                }

                return mbSaveSansCmdLine;
            }
            set
            {
                mbSaveSansCmdLine = value;
            }
        }
        private bool mbSaveSansCmdLine = true;

        /// <summary>
        /// Returns true if profile files will be read and written in XML format
        /// rather than the default line delimited "command line" format.
        /// 
        /// The default value of this property is false.
        /// </summary>
        public  bool  bUseXmlFiles
        {
            get
            {
                if ( mbAddStandardDefaults || this.ContainsKey("-XML_Profile") )
                {
                    mbUseXmlFiles = this.bValue("-XML_Profile", mbUseXmlFiles);
                }

                return mbUseXmlFiles;
            }
            set
            {
                if ( value != mbUseXmlFiles )
                {
                    if ( mbAddStandardDefaults || this.ContainsKey("-XML_Profile") )
                    {
                        this["-XML_Profile"] = value;
                    }

                    this.sLoadedPathFile = this.sReformatProfileFile(this.sLoadedPathFile);
                }

                mbUseXmlFiles = value;
            }
        }
        private bool mbUseXmlFiles = false;

        /// <summary>
        /// Returns true if all input strings are assumed to be literal while
        /// searching key strings and value strings (ie. no regular expressions).
        /// 
        /// The default value of this property is false.
        /// </summary>
        public bool bUseLiteralsOnly
        {
            get
            {
                return mbUseLiteralsOnly;
            }
            set
            {
                mbUseLiteralsOnly = value;
            }
        }
        private bool mbUseLiteralsOnly = false;

        /// <summary>
        /// The path/file location most recently used either to load the profile
        /// from a text file or to save the profile to a text file.
        /// </summary>
        public  String  sActualPathFile
        {
            get
            {
                return msActualPathFile;
            }
            set
            {
                msActualPathFile = value;
                this.bSaveEnabled = true;
            }
        }
        private String msActualPathFile;

        /// <summary>
        /// The original default file action passed to the constructor.
        /// See <see cref="tvProfileDefaultFileActions"/>.
        /// </summary>
        public  tvProfileDefaultFileActions  eDefaultFileAction
        {
            get
            {
                return meDefaultFileAction;
            }
            set
            {
                meDefaultFileAction = value;
            }
        }
        private tvProfileDefaultFileActions meDefaultFileAction = tvProfileDefaultFileActions.NoDefaultFile;

        /// <summary>
        /// The original file create action passed to the constructor.
        /// See <see cref="tvProfileFileCreateActions"/>.
        /// </summary>
        public  tvProfileFileCreateActions  eFileCreateAction
        {
            get
            {
                if ( this.ContainsKey("-NoCreate") )
                {
                    // ".Contains" is used here so that this switch
                    // is not automatically added to the profile. It
                    // can only be added to the profile manually.
                    return tvProfileFileCreateActions.NoFileCreate;
                }
                else
                {
                    return meFileCreateAction;
                }
            }
            set
            {
                meFileCreateAction = value;
            }
        }
        private tvProfileFileCreateActions meFileCreateAction = tvProfileFileCreateActions.NoFileCreate;

        /// <summary>
        /// The original "command line string" input passed to the constructor.
        /// </summary>
        public  String  sInputCommandLine
        {
            get
            {
                return msInputCommandLine;
            }
            set
            {
                msInputCommandLine = value;
            }
        }
        private String msInputCommandLine;

        /// <summary>
        /// The original "command line string array" input passed to the constructor.
        /// </summary>
        public  String[]  sInputCommandLineArray
        {
            get
            {
                return msInputCommandLineArray;
            }
            set
            {
                msInputCommandLineArray = value;
            }
        }
        private String[] msInputCommandLineArray;

        /// <summary>
        /// The path/file location of a text file just created
        /// as an unlocked backup of the current profile file.
        /// </summary>
        public  String  sBackupPathFile
        {
            get
            {
                string  lsPath = Path.GetDirectoryName(this.sLoadedPathFile);
                string  lsFilename = Path.GetFileName(this.sLoadedPathFile);
                string  lsExt = Path.GetExtension(this.sLoadedPathFile);
                string  lsBackupPathFile = Path.Combine(lsPath, lsFilename) + ".backup" + lsExt;

                bool lbSaveEnabled = this.bSaveEnabled;
                this.Save(lsBackupPathFile);
                this.sActualPathFile = this.sLoadedPathFile;
                this.bSaveEnabled = lbSaveEnabled;

                return lsBackupPathFile;
            }
        }

        /// <summary>
        /// The default file extension of the profile's text file. If
        /// <see cref="bUseXmlFiles"/> is true, this method returns ".config",
        /// otherwise it returns ".txt".
        /// </summary>
        public  String    sDefaultFileExt
        {
            get
            {
                if ( null == msDefaultFileExt )
                {
                    if ( !this.bUseXmlFiles )
                    {
                        return msDefaultFileExtArray[0];
                    }
                    else
                    {
                        return msDefaultFileExtArray[1];
                    }
                }
                else
                {
                    return msDefaultFileExt;
                }
            }
            set
            {
                msDefaultFileExt = value;
            }
        }
        private String   msDefaultFileExt;
        private String[] msDefaultFileExtArray = {mcsLoadSaveDefaultExtension, ".config"};

        /// <summary>
        /// The default path/file location of the profile's text file. This
        /// property uses <see cref="sExePathFile"/>.
        /// </summary>
        public String sDefaultPathFile
        {
            get
            {
                return this.sExePathFile + this.sDefaultFileExt;
            }
        }


        /// <summary>
        /// The path/file location of the executing application or assembly
        /// that uses the profile (including the name of the executable).
        /// This property is used by <see cref="sDefaultPathFile"/>. Setting
        /// this property allows a virtual assembly location to be used as an
        /// alternative to the actual location.
        /// </summary>
        public  String  sExePathFile
        {
            get
            {
                if ( null == msExePathFile )
                {
                    try
                    {
                        return Assembly.GetEntryAssembly().Location;
                    }
                    catch
                    {
                        return Assembly.GetExecutingAssembly().Location;
                    }
                }
                else
                {
                    return msExePathFile;
                }
            }
            set
            {
                msExePathFile = value;
            }
        }
        private String msExePathFile;

        /// <summary>
        /// The path/file location most recently used to load the profile from
        /// a text file.
        /// </summary>
        public  String  sLoadedPathFile
        {
            get
            {
                return msLoadedPathFile;
            }
            set
            {
                msLoadedPathFile = value;
                this.sActualPathFile = value;
            }
        }
        private String msLoadedPathFile;

        /// <summary>
        /// The key used to find the "key" attribute in XML "key/value" pairs
        /// (ie. "key").
        /// </summary>
        public  String  sXmlKeyKey
        {
            get
            {
                return msXmlKeyKey;
            }
            set
            {
                msXmlKeyKey = value;
            }
        }
        private String msXmlKeyKey = "key";

        /// <summary>
        /// The key used to find the "value" attribute in XML "key/value" pairs
        ///  (ie. "value").
        /// </summary>
        public  String  sXmlValueKey
        {
            get
            {
                return msXmlValueKey;
            }
            set
            {
                msXmlValueKey = value;
            }
        }
        private String msXmlValueKey = "value";

        /// <summary>
        /// The Xpath expression used to find the profile section in a given
        /// XML document. This expression must have a format similar to the
        /// default of "configuration/appSettings/add" (ie. no wildcards).
        /// </summary>
        public  String  sXmlXpath
        {
            get
            {
                return msXmlXpath;
            }
            set
            {
                msXmlXpath = value;
            }
        }
        private String msXmlXpath = "configuration/appSettings/add";

        /// <summary>
        /// The value of the item found in the profile for the given
        /// asKey, returned as a generic object. If asKey doesn't exist in the
        /// profile, it will be added with the given aoDefault object value.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value object in the profile.
        /// </param>
        /// <param name="aoDefault">
        /// The default value object added to the profile with asKey,
        /// if asKey can't be found.
        /// </param>
        /// <returns>
        /// Returns the object found for asKey or aoDefault (if asKey
        /// is not found).
        /// </returns>
        public Object GetAdd(String asKey, Object aoDefault)
        {
            Object loValue;

            if ( this.ContainsKey(asKey) )
            {
                loValue = this[asKey];
            }
            else
            {
                loValue = aoDefault;
                this.Add(asKey, loValue);

                if ( tvProfileDefaultFileActions.NoDefaultFile != this.eDefaultFileAction )
                {
                    this.Save();
                }
            }

            return loValue;
        }

        #region "Various GetAdd Return Types"

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey, cast as a boolean.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="abDefault">
        /// The boolean value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The boolean value found or abDefault (see <see cref="GetAdd"/>).
        /// </returns>
        public bool bValue(String asKey, bool abDefault)
        {
            return Convert.ToBoolean(this.GetAdd(asKey, abDefault));
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey, cast as a double.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="adDefault">
        /// The double value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The double value found or adDefault (see <see cref="GetAdd"/>).
        /// </returns>
        public double dValue(String asKey, double adDefault)
        {
            return Convert.ToDouble(this.GetAdd(asKey, adDefault));
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey, cast as a DateTime.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="adtDefault">
        /// The DateTime value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The DateTime value found or adtDefault (see <see cref="GetAdd"/>).
        /// </returns>
        public DateTime dtValue(String asKey, DateTime adtDefault)
        {
            return Convert.ToDateTime(this.GetAdd(asKey, adtDefault));
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey, cast as an integer.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="aiDefault">
        /// The integer value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The integer value found or aiDefault (see <see cref="GetAdd"/>).
        /// </returns>
        public int iValue(String asKey, int aiDefault)
        {
            return Convert.ToInt32(this.GetAdd(asKey, aiDefault));
        }

        /// <summary>
        /// The profile entry object at the given zero-based index position.
        /// </summary>
        /// <param name="aiIndex">
        /// The zero based index into the list of profile entry objects.
        /// </param>
        /// <returns>
        /// The DictionaryEntry object found at aiIndex.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <p>aiIndex is less than zero.</p>
        /// <p></p>
        /// -or-
        /// <p></p>
        /// aiIndex is equal to or greater than <see cref="ArrayList.Count"/>.
        /// </exception>
        public DictionaryEntry oEntry(int aiIndex)
        {
            return ((DictionaryEntry) base[aiIndex]);
        }

        /// <summary>
        /// Set the profile entry object at the given zero-based index position.
        /// </summary>
        /// <param name="aiIndex">
        /// The zero based index into the list of profile entry objects.
        /// </param>
        /// <param name="Value">
        /// The DictionaryEntry object to be stored at aiIndex.
        /// </param>
        public void SetEntry(int aiIndex, DictionaryEntry Value)
        {
            base[aiIndex] = Value;
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="aoDefaultProfile">
        /// The tvProfile object value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The tvProfile object value found or aoDefaultProfile (see <see cref="GetAdd"/>).
        /// </returns>
        public tvProfile oProfile(String asKey, tvProfile aoDefaultProfile)
        {
            object  loProfile = this.GetAdd(asKey, aoDefaultProfile);
            object  loProfileCast = loProfile as tvProfile;
                    if ( null == loProfileCast )
                        loProfileCast = new tvProfile(loProfile.ToString());

            return (tvProfile)loProfileCast;
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="asDefaultProfile">
        /// The tvProfile object value (converted from a command-line string)
        /// returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The tvProfile object value found or asDefaultProfile 
        /// (converted to a tvProfile object, see <see cref="GetAdd"/>).
        /// </returns>
        public tvProfile oProfile(String asKey, String asDefaultProfile)
        {
            object  loProfile = this.GetAdd(asKey, asDefaultProfile);
            object  loProfileCast = loProfile as tvProfile;
                    if ( null == loProfileCast )
                        loProfileCast = new tvProfile(loProfile.ToString());

            return (tvProfile)loProfileCast;
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <returns>
        /// The tvProfile object value found or a default empty
        /// tvProfile object will be added to the profile, see <see cref="GetAdd"/>).
        /// </returns>
        public tvProfile oProfile(String asKey)
        {
            object  loProfile = this.GetAdd(asKey, new tvProfile());
            object  loProfileCast = loProfile as tvProfile;
                    if ( null == loProfileCast )
                        loProfileCast = new tvProfile(loProfile.ToString());

            return (tvProfile)loProfileCast;
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="aoDefault">
        /// The object value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The object value found or aoDefault (see <see cref="GetAdd"/>).
        /// </returns>
        public Object oValue(String asKey, Object aoDefault)
        {
            return this.GetAdd(asKey, aoDefault);
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey,
        /// cast as a trimmed string.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="asDefault">
        /// The string value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The string value found or asDefault (see <see cref="GetAdd"/>). Any
        /// environment variables found in the return string are expanded and the
        /// return string is trimmed of leading and trailing spaces.
        /// </returns>
        public String sValue(String asKey, String asDefault)
        {
            return this.sValueNoTrim(asKey, asDefault).Trim();
        }

        /// <summary>
        /// The "<see cref="GetAdd"/> Object" value found for asKey, cast as a string (not trimmed).
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the corresponding value in the profile.
        /// </param>
        /// <param name="asDefault">
        /// The string value returned if asKey is not found.
        /// </param>
        /// <returns>
        /// The string value found or asDefault (see <see cref="GetAdd"/>). Any
        /// environment variables found in the return string are expanded and the
        /// return string is NOT trimmed of leading or trailing spaces.
        /// </returns>
        public String sValueNoTrim(String asKey, String asDefault)
        {
            String lsValue = this.GetAdd(asKey, asDefault).ToString();

            // The return limit of ExpandEnvironmentVariables is 32K. The difference allows for variable expansion.
            if ( 32000 > lsValue.Length )
            {
                return Environment.ExpandEnvironmentVariables(lsValue);
            }
            else
            {
                return lsValue;
            }
        }

        #endregion

        /// <summary>
        /// The count of profile entries with a key that matches asKey.
        /// This number will be greater than one if asKey appears multiple
        /// times in the profile (duplicate keys are OK). Likewise, this
        /// number might be greater than one if asKey contains "*" or a
        /// regular expression.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find items in the profile. "*" or a regular
        /// expression may be included.
        /// </param>
        /// <returns>
        /// Integer count of the items found.
        /// </returns>
        public int iKeyCount(String asKey)
        {
            int liCount = 0;

            if ( mbUseLiteralsOnly )
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( loEntry.Key.ToString() == asKey )
                    {
                        liCount++;
                    }
                }
            }
            else
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( Regex.IsMatch(loEntry.Key.ToString(), this.sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        liCount++;
                    }
                }
            }

            return liCount;
        }

        /// <summary>
        /// Returns the entire contents of the profile as a "command block" string
        /// (eg. <see langword='-Key1=one -Key2=2 -Key3 -Key4="we have four"'/>), 
        /// where the items are stacked vertically rather than listed horozontally.
        /// This feature is handy for minimally serializing profiles, passing them
        /// around easily and storing them elsewhere.
        /// </summary>
        /// <returns>
        /// A "command block" string (not a string array).
        /// </returns>
        public String sCommandBlock()
        {
            ++miCommandBlockRecursionLevel;

            string          lcsIndent = "".PadRight(4 * miCommandBlockRecursionLevel);
            StringBuilder   lsbCommandBlock = new StringBuilder(Environment.NewLine);

            foreach ( DictionaryEntry loEntry in this )
            {
                String lsValue = loEntry.Value.ToString();

                if ( lsValue.Contains(Environment.NewLine) )
                {
                    lsbCommandBlock.Append(lcsIndent + loEntry.Key.ToString() + "=" + mcsBeginBlockMark + Environment.NewLine);
                    lsbCommandBlock.Append(lsValue);
                    lsbCommandBlock.Append(lcsIndent + loEntry.Key.ToString() + "=" + mcsEndBlockMark + Environment.NewLine);
                }
                else
                {
                    if ( lsValue.Contains(" ") || lsValue.Contains("-") )
                    {
                        lsbCommandBlock.Append(lcsIndent + loEntry.Key.ToString() + "=" + "\"" + lsValue + "\"" + Environment.NewLine);
                    }
                    else
                    {
                        lsbCommandBlock.Append(lcsIndent + loEntry.Key.ToString() + "=" + lsValue + Environment.NewLine);
                    }
                }
            }

            lsbCommandBlock.Append(Environment.NewLine);

            --miCommandBlockRecursionLevel;

            return lsbCommandBlock.ToString();
        }
        private static int miCommandBlockRecursionLevel = 0;

        /// <summary>
        /// Returns the entire contents of the profile as a "command line" string
        /// (eg. <see langword='-Key1=one -Key2=2 -Key3 -Key4="we have four"'/>).
        /// This feature is handy for minimally serializing profiles, passing them
        /// around easily and storing them elsewhere.
        /// </summary>
        /// <returns>
        /// A "command line" string (not a string array).
        /// </returns>
        public String sCommandLine()
        {
            StringBuilder lsbCommandLine = new StringBuilder();

            foreach ( DictionaryEntry loEntry in this )
            {
                String lsValue = loEntry.Value.ToString();

                if ( lsValue.Contains(Environment.NewLine) )
                {
                    lsbCommandLine.Append(" " + loEntry.Key.ToString() + "=" + mcsBeginBlockMark + Environment.NewLine);
                    lsbCommandLine.Append(lsValue);
                    lsbCommandLine.Append(" " + loEntry.Key.ToString() + "=" + mcsEndBlockMark + Environment.NewLine);
                }
                else
                {
                    if ( lsValue.Contains(" ") || lsValue.Contains("-") )
                    {
                        lsbCommandLine.Append(" " + loEntry.Key.ToString() + "=" + "\"" + lsValue + "\"");
                    }
                    else
                    {
                        lsbCommandLine.Append(" " + loEntry.Key.ToString() + "=" + lsValue);
                    }
                }
            }

            return lsbCommandLine.ToString();
        }

        /// <summary>
        /// Returns the entire contents of the profile as a "command line" string
        /// array (eg. <see langword='-Key1=one'/>, <see langword='-Key2=2'/>,
        /// <see langword='-Key3'/>, <see langword='-Key4="we have four"'/>).
        /// </summary>
        /// <returns>
        /// A "command line" string array.
        /// </returns>
        public String[] sCommandLineArray()
        {
            String[] lsCommandLineArray = new String[this.Count];

            for ( int i = 0; i <= lsCommandLineArray.GetLength(0) - 1; i++ )
            {
                DictionaryEntry loEntry = (DictionaryEntry) base[i];

                lsCommandLineArray[i] = loEntry.Key.ToString() + "=" + loEntry.Value.ToString();
            }

            return lsCommandLineArray;
        }

        /// <summary>
        /// The profile entry key at the given zero-based index position.
        /// </summary>
        /// <param name="aiIndex">
        /// The zero based index into the list of profile entries.
        /// </param>
        /// <returns>
        /// The key string found at aiIndex.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <p>aiIndex is less than zero.</p>
        /// <p></p>
        /// -or-
        /// <p></p>
        /// aiIndex is equal to or greater than <see cref="ArrayList.Count"/>.
        /// </exception>
        public String sKey(int aiIndex)
        {
            return oEntry(aiIndex).Key.ToString();
        }

        /// <summary>
        /// Permanently replaces all hyphens in the given asSource string
        /// with another character (eg. an underscore).
        /// 
        /// This is typically used as a precursor to calling "sCommandBlock()"
        /// where embedded hyphens can't be preserved for whatever reason.
        /// </summary>
        /// <param name="asSource"></param>
        /// The source string to have hyphens replaced.
        /// <returns></returns>
        public String sSwapHyphens(String asSource)
        {
            return asSource.Replace("-", "_");
        }

        /// <summary>
        /// Returns the entire contents of the profile as an XML string. See
        /// the <see cref="sXmlXpath"/>, <see cref="sXmlKeyKey"/> and
        /// <see cref="sXmlValueKey"/> properties to learn what XML tags
        /// will be used.
        /// </summary>
        /// <param name="abStartDocument">
        /// Boolean determines if "start document" tags will be included in the
        /// XML returned.
        /// </param>
        /// <param name="abStandAlone">
        /// Boolean determines if the "stand alone" attribute will be included
        /// in the "start document" tag of the XML returned.
        /// </param>
        /// <returns>
        /// An XML string.
        /// </returns>
        public String sXml(bool abStartDocument, bool abStandAlone)
        {
            StringBuilder lsbFileAsStream = new StringBuilder();

            XmlTextWriter   loXml = new XmlTextWriter(new StringWriter(lsbFileAsStream));
                            loXml.Formatting = Formatting.Indented;

            if ( abStartDocument )
            {
                if ( abStandAlone )
                {
                    loXml.WriteStartDocument(true);
                }
                else
                {
                    // Don't even bother to write a "standalone" attribute.
                    loXml.WriteStartDocument();
                }
            }

            String[] lsXpathArray = this.sXmlXpath.Split('/');

            for ( int i = 0; i < lsXpathArray.Length; i++ )
            {
                if ( i < lsXpathArray.Length - 1 )
                {
                    loXml.WriteStartElement(lsXpathArray[i]);
                }
                else
                {
                    foreach ( DictionaryEntry loEntry in this )
                    {
                        loXml.WriteStartElement(lsXpathArray[i]);

                            bool lbTextBlock = -1 != loEntry.Value.ToString().IndexOf(Environment.NewLine);

                            loXml.WriteAttributeString(this.sXmlKeyKey, loEntry.Key.ToString());

                            if ( lbTextBlock )
                            {
                                loXml.WriteAttributeString(this.sXmlValueKey, Environment.NewLine + loEntry.Value.ToString() + Environment.NewLine);
                            }
                            else
                            {
                                loXml.WriteAttributeString(this.sXmlValueKey, loEntry.Value.ToString());
                            }

                        loXml.WriteEndElement();
                    }
                }
            }

            for ( int i = 0; i < lsXpathArray.Length - 1; i++ )
            {
                loXml.WriteEndElement();
            }

            if ( abStartDocument )
                loXml.WriteEndDocument();

            // !!!FIX THIS!!! Replace entities since they have no impact on subsequent successful XML reads.
            lsbFileAsStream.Replace("&#xD;&#xA;", Environment.NewLine);

            // !!!FIX THIS!!! Replace "utf-16" with "UTF-8" to allow current browser support.
            lsbFileAsStream.Replace("encoding=\"utf-16\"", "encoding=\"UTF-8\"");

            return lsbFileAsStream.ToString();
        }

        /// <summary>
        /// Returns a subset of the profile as a new profile using the given
        /// asKey.
        /// 
        /// The first item found with the given asKey is assumed itself to be
        /// a profile.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find the nested profile in the current
        /// profile.
        /// </param>
        /// <returns>
        /// A new object containing the nested profile found.
        /// </returns>
        public tvProfile oNestedProfile(String asKey)
        {
            tvProfile loNestedProfile = null;
            tvProfile loOneKeyProfile = this.oOneKeyProfile(asKey);

            if ( 0 == loOneKeyProfile.Count )
                loNestedProfile = new tvProfile();
            else
                loNestedProfile = new tvProfile(loOneKeyProfile[0].ToString());

            return loNestedProfile;
        }

        /// <summary>
        /// Returns a subset of the profile as a new profile. All items
        /// that match asKey will be included.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find items in the profile. "*" or a regular
        /// expression may be included.
        /// </param>
        /// <returns>
        /// A new profile object containing the items found.
        /// </returns>
        public tvProfile oOneKeyProfile(String asKey)
        {
            return this.oOneKeyProfile(asKey, false);
        }

        /// <summary>
        /// Returns a subset of the profile as a new profile. All items
        /// that match asKey will be included.
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find items in the profile. "*" or a regular
        /// expression may be included.
        /// </param>
        /// <param name="abRemoveKeyPrefix">
        /// If true and asKey contains "*" or ".*", asKey (sans the wildcards)
        /// will be removed from each key prior to its addition to the new
        /// profile.
        /// </param>
        /// <returns>
        /// A new profile object containing the items found.
        /// </returns>
        public tvProfile oOneKeyProfile(String asKey, bool abRemoveKeyPrefix)
        {
            String  lsKeyPrefixToRemove = asKey.Replace(".*","").Replace("*","");
                    if ( asKey == lsKeyPrefixToRemove || "" == lsKeyPrefixToRemove )
                    {
                        // If the given key contains no wildcards, it's not really a prefix.
                        abRemoveKeyPrefix = false;
                    }
            tvProfile loProfile = new tvProfile();

            if ( mbUseLiteralsOnly )
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    String lsKey = loEntry.Key.ToString();

                    if ( lsKey == asKey )
                    {
                        if ( abRemoveKeyPrefix )
                        {
                            loProfile.Add("-" + lsKey.Replace(lsKeyPrefixToRemove, ""), loEntry.Value);
                        }
                        else
                        {
                            loProfile.Add(lsKey, loEntry.Value);
                        }
                    }
                }
            }
            else
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    String lsKey = loEntry.Key.ToString();

                    if ( Regex.IsMatch(lsKey, this.sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        if ( abRemoveKeyPrefix )
                        {
                            loProfile.Add("-" + lsKey.Replace(lsKeyPrefixToRemove, ""), loEntry.Value);
                        }
                        else
                        {
                            loProfile.Add(lsKey, loEntry.Value);
                        }
                    }
                }
            }

            return loProfile;
        }

        /// <summary>
        /// Returns a subset of the profile as a trimmed string array.
        /// All items that match asKey are included. Note: only values are included
        /// (ie. no keys).
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find items in the profile. "*" or a regular
        /// expression may be included.
        /// </param>
        /// <returns>
        /// A string array containing the values found. Any environment variables
        /// embedded are expanded and each resulting string is trimmed of leading
        /// and trailing spaces.
        /// </returns>
        public String[] sOneKeyArray(String asKey)
        {
            StringBuilder lsbList = new StringBuilder();

            if ( mbUseLiteralsOnly )
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( loEntry.Key.ToString() == asKey )
                    {
                        String lsValue = loEntry.Value.ToString();

                        // The return limit of ExpandEnvironmentVariables is 32K. The difference allows for variable expansion.
                        if ( 32000 > lsValue.Length )
                        {
                            lsValue =  Environment.ExpandEnvironmentVariables(lsValue);
                        }

                        lsbList.Append(lsValue.Trim() + mccSplitMark);
                    }
                }
            }
            else
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( Regex.IsMatch(loEntry.Key.ToString(), this.sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        String lsValue = loEntry.Value.ToString();

                        // The return limit of ExpandEnvironmentVariables is 32K. The difference allows for variable expansion.
                        if ( 32000 > lsValue.Length )
                        {
                            lsValue =  Environment.ExpandEnvironmentVariables(lsValue);
                        }

                        lsbList.Append(lsValue.Trim() + mccSplitMark);
                    }
                }
            }

            String lsList = lsbList.ToString();

            if ( lsList.EndsWith(mccSplitMark.ToString()) )
            {
                return lsList.Remove(lsList.Length - 1, 1).Split(mccSplitMark);
            }
            else
            {
                return new String[0];
            }
        }

        /// <summary>
        /// Returns a subset of the profile as a string array (not trimmed). All
        /// items that match asKey are included. Note: only values are included
        /// (ie. no keys).
        /// </summary>
        /// <param name="asKey">
        /// The key string used to find items in the profile. "*" or a regular
        /// expression may be included.
        /// </param>
        /// <returns>
        /// A string array containing the values found. Any environment variables
        /// embedded are expanded and each resulting string is NOT trimmed of
        /// leading or trailing spaces.
        /// </returns>
        public String[] sOneKeyArrayNoTrim(String asKey)
        {
            StringBuilder lsbList = new StringBuilder();

            if ( mbUseLiteralsOnly )
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( loEntry.Key.ToString() == asKey )
                    {
                        String lsValue = loEntry.Value.ToString();

                        // The return limit of ExpandEnvironmentVariables is 32K. The difference allows for variable expansion.
                        if ( 32000 > lsValue.Length )
                        {
                            lsValue =  Environment.ExpandEnvironmentVariables(lsValue);
                        }

                        lsbList.Append(lsValue + mccSplitMark);
                    }
                }
            }
            else
            {
                foreach ( DictionaryEntry loEntry in this )
                {
                    if ( Regex.IsMatch(loEntry.Key.ToString(), this.sExpression(asKey), RegexOptions.IgnoreCase) )
                    {
                        String lsValue = loEntry.Value.ToString();

                        // The return limit of ExpandEnvironmentVariables is 32K. The difference allows for variable expansion.
                        if ( 32000 > lsValue.Length )
                        {
                            lsValue =  Environment.ExpandEnvironmentVariables(lsValue);
                        }

                        lsbList.Append(lsValue + mccSplitMark);
                    }
                }
            }

            String lsList = lsbList.ToString();

            if ( lsList.EndsWith(mccSplitMark.ToString()) )
            {
                return lsList.Remove(lsList.Length - 1, 1).Split(mccSplitMark);
            }
            else
            {
                return new String[0];
            }
        }

        /// <summary>
        /// Returns a full path/file string relative to the profile file
        /// location, if asPathFile is only a filename. Otherwise, asPathFile is
        /// returned unchanged. This feature is useful for locating ancillary
        /// files in the same folder as the profile file.
        /// </summary>
        /// <param name="asPathFile">
        /// A full path/file string or a filename only.
        /// </param>
        /// <returns>
        /// A full path/file string.
        /// </returns>
        public String sRelativeToProfilePathFile(String asPathFile)
        {
            if ( null == asPathFile )
            {
                return null;
            }

            if ( null == this.sActualPathFile )
            {
                if ( "" == Path.GetPathRoot(asPathFile) )
                {
                    return Path.Combine(Path.GetDirectoryName(this.sDefaultPathFile), asPathFile);
                }
                else
                {
                    return asPathFile;
                }
            }
            else
            {
                if ( "" == Path.GetPathRoot(asPathFile) )
                {
                    return Path.Combine(Path.GetDirectoryName(this.sActualPathFile), asPathFile);
                }
                else
                {
                    return asPathFile;
                }
            }
        }

        /// <summary>
        /// Reloads the profile from the original text file used to load it and
        /// merges in the original command line as well. Any changes to the
        /// profile in memory (not saved) since the last load will be lost.
        /// </summary>
        public void Reload()
        {
            this.Clear();

            if ( null != this.sLoadedPathFile )
                this.Load(this.sLoadedPathFile , tvProfileLoadActions.Append);

            this.LoadFromCommandLineArray(this.sInputCommandLineArray, tvProfileLoadActions.Merge);
        }

        /// <summary>
        /// Loads the profile with items from the given text file.
        /// <p>
        /// If <see cref="bUseXmlFiles"/> is true, text files will be read assuming
        /// standard XML "configuration file" format rather than line delimited
        /// "command line" format.
        /// </p>
        /// </summary>
        /// <param name="asPathFile">
        /// The path/file location of the text file to load. This value will
        /// be used to set <see cref="sLoadedPathFile"/> after a successful load.
        /// </param>
        /// <param name="aeLoadAction">
        /// The action to take while loading profile items.
        /// See <see cref="tvProfileLoadActions"/>
        /// </param>
        public void Load(

                  String asPathFile
                , tvProfileLoadActions aeLoadAction
                )
        {
            // If asPathFile is not null, check existence. Otherwise check for the existence
            // of one of several default filenames. Returned null means none exist.
            String  lsPathFile = this.sFileExistsFromList(this.sRelativeToProfilePathFile(asPathFile));
            String  lsFilnameOnly = Path.GetFileNameWithoutExtension(this.sExePathFile);
            String  lcsLoadingMsg = lsFilnameOnly + " loading, please wait ...";

            if ( null == lsPathFile )
            {
                if ( null == asPathFile )
                {
                    lsPathFile = this.sDefaultPathFile;
                }
                else
                {
                    lsPathFile = asPathFile;
                }

                if ( tvProfileFileCreateActions.PromptToCreateFile == this.eFileCreateAction )
                {
                    // If the EXE is not already in a folder with a matching name and if the 
                    // EXE is not already installed in a typical installation folder, proceed.
                    if ( !this.bInOwnFolder && !this.bInstalledAlready )
                    {
                        String lsNewPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), lsFilnameOnly);
                        String lsMessage = String.Format(( Directory.Exists(lsNewPath)
                                ? "an existing folder ({0}) on your desktop"
                                : "a new folder ({0}) on your desktop" ), lsFilnameOnly);

                        if ( DialogResult.OK == MessageBox.Show(String.Format(@"
For your convenience, this program will be copied
to {0}.

Depending on your system, this may take several seconds.  

Copy and proceed from there?

"
                                    , lsMessage), "Copy EXE to Desktop?", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) )
                        {
                            if ( null != mttvMessageBox )
                            {
                                moAppLoadingWaitMsg = Activator.CreateInstance(mttvMessageBox);
                                mttvMessageBox.InvokeMember("ShowWait", BindingFlags.InvokeMethod, null, moAppLoadingWaitMsg
                                                            , new object[]{null, lcsLoadingMsg, 250});
                            }

                            String lsNewExePathFile = Path.Combine(lsNewPath, Path.GetFileName(this.sExePathFile));

                            if ( !Directory.Exists(lsNewPath) )
                                Directory.CreateDirectory(lsNewPath);

                            File.Copy(this.sExePathFile, lsNewExePathFile, true);

                            ProcessStartInfo    loStartInfo = new ProcessStartInfo(lsNewExePathFile);
                                                loStartInfo.WorkingDirectory = Path.GetDirectoryName(lsNewExePathFile);
                            Process             loProcess = Process.Start(loStartInfo);

                            //this.bExit = true;        // This was moved outside the block after disabling the other show below.
                        }

                        this.bExit = true;
                    }

                    //if ( !this.bExit )
                    //{
                    //    if ( !this.bInOwnFolder && DialogResult.Cancel == MessageBox.Show(String.Format(
                    //                  "The profile file for this program can't be found ({0}).\n\n"
                    //                + "Create it and proceed anyway?", lsPathFile)
                    //                        , "Create Profile File?"
                    //                , MessageBoxButtons.OKCancel, MessageBoxIcon.Question) )
                    //    {
                    //        this.bExit = true;
                    //    }
                    //}
                }

                // Although technically the file was not loaded (since it doesn't exist yet),
                // we consider it empty and we set the property to allow for subsequent saves.
                if ( !this.bExit )
                    this.sLoadedPathFile = lsPathFile;
            }
            else
            {
                if ( null != mttvMessageBox )
                {
                    moAppLoadingWaitMsg = Activator.CreateInstance(mttvMessageBox);
                    mttvMessageBox.InvokeMember("ShowWait", BindingFlags.InvokeMethod, null, moAppLoadingWaitMsg
                                                , new object[]{null, lcsLoadingMsg, 250});
                }

                String  lsFileAsStream = null;
                        this.UnlockProfileFile();

                        StreamReader loStreamReader = null;

                        try
                        {
                            loStreamReader = new StreamReader(lsPathFile);
                            lsFileAsStream = loStreamReader.ReadToEnd();
                        }
                        catch (IOException ex)
                        {
                            if ( !ex.Message.Contains("being used by another process") )
	                        {
                                // Wait a moment ...
                                System.Threading.Thread.Sleep(200);

                                // Then try again.
                                if ( null != loStreamReader )
                                    loStreamReader.Close();

                                loStreamReader = new StreamReader(lsPathFile);
                                lsFileAsStream = loStreamReader.ReadToEnd();
	                        }
                        }
                        finally
                        {
                            if ( null != loStreamReader )
                                loStreamReader.Close();
                        }

                        if ( !this.bLockProfileFile(lsPathFile) )
                        {
                            this.bExit = true;
                            return;
                        }

                int liDoOver = 1;

                do
                {
                    // mbUseXmlFiles is intentionally used here (instead of "this.bUseXmlFiles") to avoid side effects.
                    if ( mbUseXmlFiles )
                    {
                        try
                        {
                            this.LoadFromXml(lsFileAsStream, aeLoadAction);

                            // The profile file format is as expected. No do-over is needed.
                            liDoOver = 0;

                            if ( !this.bUseXmlFiles )
                            {
                                lsPathFile = this.sReformatProfileFile(lsPathFile);
                            }
                        }
                        catch
                        {
                            // mbUseXmlFiles is intentionally used here (instead of "this.bUseXmlFiles") to avoid side effects.
                            mbUseXmlFiles = false;
                        }
                    }

                    // mbUseXmlFiles is intentionally used here (instead of "this.bUseXmlFiles") to avoid side effects.
                    if ( !mbUseXmlFiles )
                    {
                        if ( lsFileAsStream.Length > 11 )
                        {
                            // Look for the XML tag only near the top (an XML document could be embedded way below).
                            if ( -1 == lsFileAsStream.IndexOf("<?xml versi", 0, 11) )
                            {
                                // The profile file format is as expected. No do-over is needed.
                                liDoOver = 0;

                                // The default file format is line delimited "command line" format.
                                // The use of all known line delimiters allows for passing profiles between environments.
                                this.LoadFromCommandLineArray(lsFileAsStream.Replace("\r\n", mccSplitMark.ToString())
                                        .Replace("\n", mccSplitMark.ToString()).Split(mccSplitMark), aeLoadAction);

                                if ( this.bUseXmlFiles )
                                {
                                    lsPathFile = this.sReformatProfileFile(lsPathFile);
                                }
                            }
                            else
                            {
                                // mbUseXmlFiles is intentionally used here (instead of "this.bUseXmlFiles") to avoid side effects.
                                mbUseXmlFiles = true;
                            }
                        }
                    }
                }
                while ( liDoOver-- > 0 );

                this.sLoadedPathFile = lsPathFile;
            }

            // If it doesn't already exist, create the file.
            if ( !this.bExit
                    && !File.Exists(lsPathFile)
                    && tvProfileFileCreateActions.NoFileCreate != this.eFileCreateAction )
            {
                this.Save(lsPathFile);

                // In this case, consider the file loaded also.
                this.sLoadedPathFile = lsPathFile;
            }
        }

        /// <summary>
        /// Loads the profile with items from the given "command line" string.
        /// </summary>
        /// <param name="asCommandLine">
        /// A string (not a string array) of the form:
        /// <see langword='-Key1=one -Key2=2 -Key3 -Key4="we have four"'/>.
        /// </param>
        /// <param name="aeLoadAction">
        /// The action to take while loading profile items.
        /// See <see cref="tvProfileLoadActions"/>
        /// </param>
        public void LoadFromCommandLine(

                  String asCommandLine
                , tvProfileLoadActions aeLoadAction
                )
        {
            if ( null == asCommandLine )
                return;

            // Remove any leading spaces or tabs so that mccSplitMark becomes the first char.
            asCommandLine = asCommandLine.TrimStart(' ');
            asCommandLine = asCommandLine.TrimStart('\t');

            if ( -1 != asCommandLine.IndexOf('\n') )
            {
                // If the command line is actually already line delimited, then we're practically done.
                // The use of all known line delimiters allows for passing profiles between environments.
                this.LoadFromCommandLineArray(asCommandLine.Replace("\r\n", mccSplitMark.ToString())
                        .Replace("\n", mccSplitMark.ToString()).Split(mccSplitMark), aeLoadAction);
            }
            else
            {
                StringBuilder lsbNewCommandLine = new StringBuilder();

                char lcCurrent = '\u0000';
                bool lbQuoteOn = false;

                for ( int i = 0; i <= asCommandLine.Length - 1; i++ )
                {
                    char    lcPrevious;
                            lcPrevious = lcCurrent;
                            lcCurrent = asCommandLine[i];
                            if ( '\"' == lcCurrent )
                            {
                                lbQuoteOn = ! lbQuoteOn;
                            }

                    if ( lbQuoteOn || '-' != lcCurrent || '=' == lcPrevious )
                    {
                        // The "|| '=' == lcPrevious" allows for negative numbers.
                        lsbNewCommandLine.Append(lcCurrent);
                    }
                    else if ( '\\' == lcPrevious )
                    {
                        lsbNewCommandLine.Remove(lsbNewCommandLine.Length - 1, 1);
                        lsbNewCommandLine.Append(lcCurrent);
                    }
                    else
                    {
                        lsbNewCommandLine = new StringBuilder(lsbNewCommandLine.ToString().Trim() + mccSplitMark + "-");
                    }
                }

                //Cleanup any remaining tabs.
                lsbNewCommandLine.Replace('\t', ' ');

                //The first occurrence of the separator must be removed.
                this.LoadFromCommandLineArray(lsbNewCommandLine.ToString()
                        .TrimStart(mccSplitMark).Split(mccSplitMark), aeLoadAction);
            }
        }

        /// <summary>
        /// Loads the profile with items from the given "command line"
        /// string array.
        /// </summary>
        /// <param name="asCommandLineArray">
        /// A string array of the form: <see langword='-Key1=one'/>, <see langword='-Key2=2'/>,
        /// <see langword='-Key3'/>, <see langword='-Key4="we have four"'/>.
        /// </param>
        /// <param name="aeLoadAction">
        /// The action to take while loading profile items.
        /// See <see cref="tvProfileLoadActions"/>
        /// </param>
        public void LoadFromCommandLineArray(

                  String[] asCommandLineArray
                , tvProfileLoadActions aeLoadAction
                )
        {
            if ( tvProfileLoadActions.Overwrite == aeLoadAction )
            {
                this.Clear();
            }

            if ( null != asCommandLineArray )
            {
                String lsBlockKey = null;
                String lsBlockValue = null;
                Hashtable loMergeKeysMap = new Hashtable();

                foreach ( String lsItem in asCommandLineArray )
                {
                    if ( !lsItem.TrimStart().StartsWith("-") )
                    {
                        if ( null != lsBlockKey )
                        {
                            lsBlockValue += lsItem + Environment.NewLine;
                        }

                        // If an item does not start with a "-"
                        // and is not within a block, ignore it.
                    }
                    else
                    {
                        String lsKey;
                        String lsValue;
                        Object loValue;
                        int liPos = lsItem.IndexOf("=");

                        if ( -1 == liPos )
                        {
                            lsKey = lsItem.Trim();
                            loValue = true;
                        }
                        else
                        {
                            lsKey = lsItem.Substring(0, liPos).Trim();
                            lsValue = lsItem.Substring(liPos + 1).Trim();

                            if ( lsValue.StartsWith("\"") && lsValue.EndsWith("\"") )
                            {
                                if ( lsValue.Length < 2 )
                                    loValue = "";
                                else
                                    loValue = lsValue.Substring(1, lsValue.Length - 2);
                            }
                            else
                            {
                                // This is intentionally not trimmed.
                                loValue = lsItem.Substring(liPos + 1);
                            }
                        }

                        lsValue = loValue.ToString();

                        if ( null != lsBlockKey )
                        {
                            if ( mcsEndBlockMark == lsValue && lsBlockKey == lsKey )
                            {
                                lsBlockKey = null;
                                loValue = lsBlockValue;
                            }
                            else
                            {
                                lsBlockValue += lsItem + Environment.NewLine;
                            }
                        }
                        else if ( mcsBeginBlockMark == lsValue )
                        {
                            lsBlockKey = lsKey;
                            lsBlockValue = null;
                        }

                        if ( null == lsBlockKey )
                        {
                            switch ( aeLoadAction )
                            {
                                case tvProfileLoadActions.Append:
                                case tvProfileLoadActions.Overwrite:

                                    this.Add(lsKey, loValue);
                                    break;

                                case tvProfileLoadActions.Merge:

                                    // Only disable saving after merges if the "bSaveSansCmdLine" switch is turned off.
                                    // Likewise, only after merges do we bother to check for command line keys to remove.
                                    if ( !this.bSaveSansCmdLine )
                                        this.bSaveEnabled = false;

                                    int liIndex = this.IndexOfKey(lsKey);

                                    // Replace wildcard keys with the first key match, if any.
                                    lsKey = ( -1 == liIndex ? lsKey : this.sKey(liIndex) );

                                    if ( loMergeKeysMap.ContainsKey(lsKey) )
                                    {
                                        // Set the search index to force adding this key.
                                        liIndex = -1;
                                    }
                                    else
                                    {
                                        if ( -1 != liIndex )
                                        {
                                            // Remove all previous entries with this key (presumably from a file).
                                            this.Remove(lsKey);

                                            // Set the search index to force adding this key with its overriding value.
                                            liIndex = -1;
                                        }

                                        // Add to the merge key map to prevent any further removals of this key.
                                        loMergeKeysMap.Add(lsKey, null);
                                    }

                                    if ( -1 == liIndex )
                                    {
                                        // Don't add keys that contain '*'.
                                        if ( -1 == lsKey.IndexOf('*') )
                                            this.Add(lsKey, loValue);
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads the profile with items from the given XML string. See the
        /// <see cref="sXmlXpath"/>, <see cref="sXmlKeyKey"/> and
        /// <see cref="sXmlValueKey"/> properties to learn what XML tags are
        /// expected.
        /// </summary>
        /// <param name="asXml">
        /// An XML document as a string.
        /// </param>
        /// <param name="aeLoadAction">
        /// The action to take while loading profile items.
        /// See <see cref="tvProfileLoadActions"/>
        /// </param>
        public void LoadFromXml(

                  String asXml
                , tvProfileLoadActions aeLoadAction
                )
        {
            if ( tvProfileLoadActions.Overwrite == aeLoadAction )
            {
                this.Clear();
            }

            XmlDocument loXmlDoc = new XmlDocument();
                        loXmlDoc.LoadXml(asXml);

            foreach ( XmlNode loNode in loXmlDoc.SelectNodes(this.sXmlXpath) )
            {
                String lsKey = loNode.Attributes[this.sXmlKeyKey].Value;
                String lsValue = loNode.Attributes[this.sXmlValueKey].Value.StartsWith(Environment.NewLine)
                        ? loNode.Attributes[this.sXmlValueKey].Value.Substring(Environment.NewLine.Length
                                , loNode.Attributes[this.sXmlValueKey].Value.Length - 2 * Environment.NewLine.Length)
                        : loNode.Attributes[this.sXmlValueKey].Value;

                switch ( aeLoadAction )
                {
                    case tvProfileLoadActions.Append:
                    case tvProfileLoadActions.Overwrite:

                        this.Add(lsKey, lsValue);
                        break;

                    case tvProfileLoadActions.Merge:

                        this.bSaveEnabled = false;

                        int liIndex = this.IndexOfKey(lsKey);

                        if ( -1 != liIndex )
                        {
                            // Set each entry with a matching key to the given value.

                            foreach ( DictionaryEntry loEntry in this.oOneKeyProfile(lsKey, false) )
                            {
                                this.SetByIndex(this.IndexOfKey(loEntry.Key.ToString()), lsValue);
                            }
                        }
                        else
                        {
                            // Don't add keys that contain '*'.
                            if ( -1 == lsKey.IndexOf('*') )
                                this.Add(lsKey, lsValue);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Presents a grid UI for editing the contents of this profile object.
        /// Well known application independent tvProfile parameters are excluded.
        /// </summary>
        public void Edit()
        {
            this.Edit(this);
        }

        /// <summary>
        /// Presents a grid UI for editing the contents of a profile object.
        /// Well known application independent tvProfile parameters are excluded.
        /// </summary>
        /// <param name="aoProfile">
        /// The given profile object to edit.
        /// </param>
        public void Edit(tvProfile aoProfile)
        {
            // This is done via late-binding so that the editor class files
            // are not required for successful compilation if they are not used.
            Type    loType = Assembly.GetExecutingAssembly().GetType("tvToolbox.tvProfileEditor", true);
            Object  loEditor = Activator.CreateInstance(loType, aoProfile);

            loType.GetMethod("ShowDialog", Type.EmptyTypes).Invoke(loEditor, null);
        }

        /// <summary>
        /// Saves the contents of the profile as a text file.
        /// </summary>
        /// <param name="asPathFile">
        /// The path/file location to save the profile file to. This value will
        /// be used to set <see cref="sActualPathFile"/> after a successful save.
        /// </param>
        public void Save(String asPathFile)
        {
            this.sActualPathFile = asPathFile;
            this.Save();
        }

        /// <summary>
        /// <p>
        /// Saves the contents of the profile as a text file using the location
        /// referenced in <see cref="sActualPathFile"/>. <see cref="sActualPathFile"/>
        /// will have the same value as <see cref="sLoadedPathFile"/> after a successful
        /// load from a text file.
        /// </p>
        /// <p>
        /// If <see cref="bUseXmlFiles"/> is true, text files will be written in
        /// standard XML "configuration file" format rather than line delimited
        /// "command line" format.
        /// </p>
        /// </summary>
        public void Save()
        {
            if ( !this.bSaveEnabled || null == this.sActualPathFile )
            {
                return;
            }

            String lsFileAsStream = null;

            if ( this.bUseXmlFiles )
            {
                lsFileAsStream = this.sXml(true, false);
            }
            else
            {
                StringBuilder lsbFileAsStream = new StringBuilder(Path.GetFileName(this.sExePathFile) + Environment.NewLine + Environment.NewLine);

                foreach ( DictionaryEntry loEntry in this )
                {
                    String lsKey = loEntry.Key.ToString();
                    String lsValue = loEntry.Value.ToString();

                    // "mbSaveSansCmdLine" is referenced here directly to gain a little speed.
                    if ( !mbSaveSansCmdLine || null == moInputCommandLineProfile
                            || (mbSaveSansCmdLine && !moInputCommandLineProfile.ContainsKey(lsKey)) )
                    {
                        if ( -1 == lsValue.IndexOf(Environment.NewLine) )
                        {
                            if ( -1 == lsValue.IndexOf(" ") )
                                lsbFileAsStream.Append(lsKey + "=" + lsValue + Environment.NewLine);
                            else
                                lsbFileAsStream.Append(lsKey + "=" + "\"" + lsValue + "\"" + Environment.NewLine);
                        }
                        else
                        {
                            lsbFileAsStream.Append(lsKey + "=" + mcsBeginBlockMark + Environment.NewLine);
                            lsbFileAsStream.Append(lsValue + ((lsValue.EndsWith(Environment.NewLine)) ? "" : Environment.NewLine).ToString());
                            lsbFileAsStream.Append(lsKey + "=" + mcsEndBlockMark + Environment.NewLine);
                        }
                    }
                }

                lsFileAsStream = lsbFileAsStream.ToString();
            }

            bool lbAlreadyThere = File.Exists(this.sActualPathFile);

            this.UnlockProfileFile();

            StreamWriter loStreamWriter = null;

            try
            {
                loStreamWriter = new StreamWriter(this.sActualPathFile, false);
                loStreamWriter.Write(lsFileAsStream);
            }
            catch (Exception)
            {
                // Wait a moment ...
                System.Windows.Forms.Application.DoEvents();
                System.Threading.Thread.Sleep(200);

                // Then try again.
                if ( null != loStreamWriter )
                    loStreamWriter.Close();

                loStreamWriter = new StreamWriter(this.sActualPathFile, false);
                loStreamWriter.Write(lsFileAsStream);
            }
            finally
            {
                if ( null != loStreamWriter )
                    loStreamWriter.Close();
            }

            //this.bLockProfileFile(this.sActualPathFile);

            if ( !lbAlreadyThere )
                this.bFileJustCreated = true;
        }

        /// <summary>
        /// A custom enumerator. This is necessary since the indexer of this class
        /// returns the object found by index or by key rather than the underlying
        /// DictionaryEntry object that contains the "key/value" pair.
        /// </summary>
        public override IEnumerator GetEnumerator()
        {
            return new tvProfileEnumerator(this);
        }

        /// <summary>
        /// This exception is thrown by <see langword='Add(Object)'/> if the given object
        /// is anything other than a DictionaryEntry.
        /// </summary>
        public class InvalidAddType : Exception
        {
            /// <summary>
            /// Returns "Only DictionaryEntry objects may be added with this method."
            /// </summary>
            public override String Message
            {
                get
                {
                    return "Only DictionaryEntry objects may be added with this method.";
                }
            }

        }

        #region "Private Members"

        private void ReplaceDefaultProfileFromCommandLine(String[] asCommandLineArray)
        {
            this.LoadFromCommandLineArray(asCommandLineArray, tvProfileLoadActions.Overwrite);

            String[] lsIniKeys = new String[] { "-ini", "-ProfileFile" };

            int     liIniKeyIndex = - 1;
                    if (      this.ContainsKey(lsIniKeys[0]) )
                    {
                        liIniKeyIndex = 0;
                    }
                    else if ( this.ContainsKey(lsIniKeys[1]) )
                    {
                        liIniKeyIndex = 1;
                    }
            String  lsProfilePathFile = null;
                    if ( -1 != liIniKeyIndex )
                    {
                        lsProfilePathFile = this.sValue(lsIniKeys[liIniKeyIndex], "");
                    }
            bool    lbFirstArgIsFile = false;
            String  lsFirstArg = null;
                    try
                    {
                        if ( -1 != this.sInputCommandLineArray[0].IndexOf(".vshost.")
                                || this.sInputCommandLineArray[0] == this.sExePathFile )
                        {
                            lsFirstArg = this.sInputCommandLineArray[1];
                        }
                        else
                        {
                            lsFirstArg = this.sInputCommandLineArray[0];
                        }
                    }
                    catch {}

            if ( null != lsFirstArg
                    && File.Exists(this.sRelativeToProfilePathFile(lsFirstArg)) )
            {
                if ( null != lsProfilePathFile )
                {
                    // If the first argument passed on the command line is actually
                    // a file (that exists) and if an -ini key was also provided, then
                    // add the file reference to the profile using the "-File" key.
                    lbFirstArgIsFile = true;
                }
                else
                {
                    // If no -ini key was passed, then assume the referenced file is
                    // actually a profile file to be loaded.
                    lsProfilePathFile = lsFirstArg;
                }
            }

            if ( null != lsProfilePathFile )
            {
                // Load the referenced profile file.
                tvProfile   loNewProfile = new tvProfile();
                            loNewProfile.eFileCreateAction = this.eFileCreateAction;
                            loNewProfile.bUseXmlFiles = this.bUseXmlFiles;
                            loNewProfile.bAddStandardDefaults = this.bAddStandardDefaults;
                            loNewProfile.Load(lsProfilePathFile, tvProfileLoadActions.Overwrite);

                            this.sActualPathFile = loNewProfile.sActualPathFile;
                            this.sLoadedPathFile = loNewProfile.sLoadedPathFile;
                            this.bExit = loNewProfile.bExit;

                if ( !this.bExit )
                {
                    this.bFileJustCreated = loNewProfile.bFileJustCreated;

                    // We now need a slightly modified version of the given command line
                    // (ie. sans the -ini key but with a -File key, if appropriate).
                    tvProfile   loCommandLine = new tvProfile();
                                loCommandLine.LoadFromCommandLineArray(
                                        this.sInputCommandLineArray, tvProfileLoadActions.Overwrite);
                                if ( -1 != liIniKeyIndex )
                                    loCommandLine.Remove(lsIniKeys[liIniKeyIndex]);
                                if ( lbFirstArgIsFile )
                                    loCommandLine.Add("-File", lsFirstArg);


                    // Now merge in the original command line (with the above
                    // adjustments). Command line items take precedence over file items.
                    loNewProfile.LoadFromCommandLineArray(loCommandLine.sCommandLineArray(), tvProfileLoadActions.Merge);
                    this.bSaveEnabled = loNewProfile.bSaveEnabled;

                    // Reinitiallize the profile with the new combined results.
                    this.LoadFromCommandLineArray(loNewProfile.sCommandLineArray(), tvProfileLoadActions.Overwrite);
                    this.bDefaultFileReplaced = true;
                }

                loNewProfile.UnlockProfileFile();
            }
        }

        private String sExpression(String asSource)
        {
            String lsExpression = asSource;

            if ( -1 == lsExpression.IndexOf(".*") )
                lsExpression = lsExpression.Replace("*", ".*");

            if ( -1 == lsExpression.IndexOf("$") )
                lsExpression = lsExpression + "$";

            return lsExpression;
        }

        private String sFileExistsFromList(String asPathFile)
        {
            if ( null != asPathFile )
            {
                if ( File.Exists(asPathFile) )
                {
                    return asPathFile;
                }
            }
            else
            {
                String lsDefaultPathFileNoExt = Path.Combine(Path.GetDirectoryName(this.sDefaultPathFile)
                                                        , Path.GetFileNameWithoutExtension(this.sDefaultPathFile));

                foreach ( String lsItem in msDefaultFileExtArray )
                {
                    String lsDefaultPathFile = lsDefaultPathFileNoExt + lsItem;

                    if ( File.Exists(lsDefaultPathFile) )
                        return lsDefaultPathFile;
                }
            }

            return null;
        }

        private String sReformatProfileFile(String asPathFile)
        {
            if ( null != asPathFile)
            {
                if ( this.bDefaultFileReplaced )
                {
                    // Reuse the replacement pathfile.
                    this.Save();
                }
                else
                {
                    String lsPreviousPathFile = asPathFile;

                    // Use a new pathfile (perhaps).
                    asPathFile = this.sDefaultPathFile;
                    this.Save(asPathFile);

                    if ( this.bSaveEnabled )
                        File.Delete(lsPreviousPathFile);
                }
            }

            return asPathFile;
        }

        private bool bLockProfileFile(string asPathFile)
        {
            bool lbLockProfileFile = false;

            if ( null != moFileStreamProfileFileLock || !this.bEnableFileLock )
            {
                lbLockProfileFile = true;
            }
            else
            {
                try
                {
                    moFileStreamProfileFileLock =
                            File.Open(asPathFile, FileMode.Open, FileAccess.Read, FileShare.None);

                    lbLockProfileFile = true;
                }
                catch {/* Most likely trying to run more than one instance. Let main app handle it. */}
            }

            return lbLockProfileFile;
        }

        private void UnlockProfileFile()
        {
            if ( null != moFileStreamProfileFileLock )
            {
                moFileStreamProfileFileLock.Close();
                moFileStreamProfileFileLock.Dispose();
                moFileStreamProfileFileLock = null;
                GC.Collect();
            }
        }


        private const String mcsLoadSaveDefaultExtension = ".txt";
        private const String mcsBeginBlockMark = "[";
        private const String mcsEndBlockMark = "]";
        private const char   mccSplitMark = '\u0001';
        private FileStream   moFileStreamProfileFileLock;
        private tvProfile    moInputCommandLineProfile;
        private Type         mttvMessageBox = null;
        private object       moAppLoadingWaitMsg;


        private class tvProfileEnumerator : IEnumerator
        {
            int miIndex = -1;
            tvProfile moProfile;

            private tvProfileEnumerator(){}

            public tvProfileEnumerator(tvProfile aoProfile)
            {
                moProfile = aoProfile;
            }

            #region IEnumerator Members

            public void Reset()
            {
                miIndex = -1;
            }

            public Object Current
            {
                get
                {
                    return moProfile.oEntry(miIndex);
                }
            }

            public bool MoveNext()
            {
                miIndex++;
                return miIndex < moProfile.Count;
            }

            #endregion
        }
        #endregion
    }
}
