# tss

Rough draft of a language suited for visiting tree nodes.

## Syntax specification

```
<stylesheet>           :== <declaration>*
<declaration>          :== <script-declaration> | <style-declaration>
<script-declaration>   :== '<?' <script> '?>'
<style-statement>      :== <or-selector> '{' <statement>* '}'
<statement>            :== <assignment> | <declaration>
<assignment>           :== <literal> ':' <literal> ';'
<or-selector>          :== <containment-selector> (',' <containment-selector>)*
<containment-selector> :== ('&' <whitespace>?)? <and-selector> (<whitespace> <and-selector>)*
<and-selector>         :== <not-selector> ('^'? <not-selector>)*
<not-selector>         :== '!'? <atom-selector>
<atom-selector>        :== '*' | <literal> | <script-declaration>
<literal>              :== <identifier> | <string>
# <string> and <identifier> are tokens.
# <script> is any sequence of a scripting language.
# <whitespace> is any sequence of one or more whitespace tokens.
```

## Example

This language is useful for batch processing of Excel documents.

Example:

```
<?
console.log('This is a script inside a stylesheet.')
?>

/* For each workbook, take the first sheet. */
workbook sheet$1 {
  <? console.log(`Currently styling sheet: ${this.name}.`) ?>

  /* Give the whole sheet black text and white background. /*
  font-color: black;
  fill: white;

  /* For each column in the sheet that has a width smaller than 10 */
  col<? this.width < 10 ?> {
    /* Give it a width of 10. */
    width: 10;
  }

  <? let total = 0 ?>

  /* For each row, take the first cell, only if it contains a numeric value. */
  tr td$1<? !isNaN(this.value) ?> {
    /* Add the cell's value to the total. */
    <? total += parseFloat(this.value) ?>

    /* Give the cell a blue fill. */
    fill: blue;
  }

  /* Print total value. */
  <? console.log($`Total value is ${total}`) ?>
}

/* Style the tables only if we passed 'styleTables:true' to the stylesheet. */
<? if (args.styleTables) { ?>
  /* For each parsed table with class 'stats'. */
  table.stats {
    /* Style the table's head. */
    thead {
      /* Head cells get a bold font */
      font-bold: true;
  
      /* Last row of thead gets a bottom border */
      tr$last {
        border-bottom-style: medium;
        border-bottom-color: black;
      }
    }
  
    /* For each even row in the table's body */
    tbody tr$even {
      /* Give the table a striped look. */
      fill: #ffc4cdff;
    }
  }
<? } ?>

<?
console.log('Finished processing document')
?>
```

An example stylesheet used in production, which normalizes the look of some tables.

```
table {
  vertical-alignment: center;
  horizontal-alignment: center;
  text-wrap: false;
  border-top-style: thin;
  border-right-style: thin;
  border-bottom-style: thin;
  border-left-style: thin;
  border-top-color: black;
  border-right-color: #FFAAAAAA;
  border-bottom-color: black;
  border-left-color: #FFAAAAAA;
  font-bold: false;
  font-size: 16;
  font-name: Arial;

  tr {
    height: 23.25;
  }

  thead {
    tr$1 {
      border-top-color: black;
      border-top-style: medium;
    }

    tr$last {
      border-bottom-color: black;
      border-bottom-style: medium;
    }
  }

  tbody {
    tr$1 {
      border-top-color: black;
      border-top-style: medium;
    }

    tr$last {
      border-bottom-color: black;
      border-bottom-style: medium;
    }
  }

  td$1 {
    border-left-color: black;
    border-left-style: medium;
  }

  td$last {
    border-right-color: black;
    border-right-style: medium;
  }
}

table.title {
  thead tr$1 {
    td$1 {
      font-size: 20;
    }

    font-bold: true;
    height: 45;
    wrap-text: true;
  }

  &$1 thead tr$1 td$1 {
    fill: black;
    font-color: white;
  }
}

table!.title {
  font-size: 18;

  thead {
    font-bold: true;
    fill: #FFF0F0F0;
  }

  tbody {
    tr$odd {
      font-bold: true;
    }
  }
}

collection {
  <? let merges ?>

  table {
    &.title {
      <? merges = new Set() ?>
      td<? this.columnSpan > 1 ?> {
        <? merges.add(this.number) ?>
      }
    }

    td<? merges.has(this.number) ?> {
      border-left-style: medium;
      border-left-color: black;
    }
  }
}
```
